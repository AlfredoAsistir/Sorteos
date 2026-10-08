#nullable enable
using System.Data;
using System.Security.Cryptography;
using eLotto.Core.Data;
using eLotto.Core.Models;
using eLotto.Core.Services;
using eLotto.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace eLotto.Services;

public sealed record SorteoTransparencyStatus(int SorteoId, DateTime FechaProgramada, string Estado);

public interface ISorteoTransparencyService
{
    Task<SorteoTransparencyStatus?> GetStatusAsync(int sorteoId, CancellationToken cancellationToken);
    Task<(byte[] Content, string Hash)?> GetPdfAsync(int sorteoId, CancellationToken cancellationToken);
}

public sealed class SorteoTransparencyService : ISorteoTransparencyService
{
    private const int ToleranceMinutes = 15;
    private readonly eLottoContext _context;
    private readonly ILogger<SorteoTransparencyService> _logger;
    private readonly ISorteoTimeService _sorteoTimeService;
    private readonly ISorteoTransparencyFileStore _files;
    private readonly string _applicationName;
    private readonly int _publicationMinutes;
    private readonly int _closeMinutes;

    public SorteoTransparencyService(eLottoContext context, ILogger<SorteoTransparencyService> logger,
        IOptions<LotteryRulesOptions> options, ISorteoTimeService sorteoTimeService,
        ISorteoTransparencyFileStore files, IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _sorteoTimeService = sorteoTimeService;
        _files = files;
        _applicationName = configuration["Branding:ApplicationName"]?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_applicationName))
            throw new InvalidOperationException("Branding:ApplicationName is not configured.");
        _closeMinutes = options.Value.SalesCloseMinutesBeforeDraw;
        if (_closeMinutes <= ToleranceMinutes)
            throw new InvalidOperationException("El cierre de ventas debe permitir 15 minutos de tolerancia para transparencia.");
        _publicationMinutes = _closeMinutes - ToleranceMinutes;
    }

    public async Task<SorteoTransparencyStatus?> GetStatusAsync(int sorteoId, CancellationToken cancellationToken)
    {
        var sorteo = await _context.Sorteos.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == sorteoId, cancellationToken);
        if (sorteo == null) return null;

        var scheduledAt = _sorteoTimeService.ResolveScheduledTime(sorteo);
        var published = _files.Exists(sorteo);
        var now = ApplicationClock.NowOffset;
        if (now < scheduledAt.AddMinutes(-_closeMinutes))
            return new(sorteoId, sorteo.Fecha, published ? "publicado" : "pendiente");
        if (!string.IsNullOrWhiteSpace(sorteo.NumeroGanador))
            return new(sorteoId, sorteo.Fecha, published ? "publicado" : "no_aplica");
        if (published || now < scheduledAt.AddMinutes(-_publicationMinutes))
        {
            var soldCount = await _context.BoletosConfirmados.AsNoTracking()
                .CountAsync(x => x.SorteosId == sorteoId, cancellationToken);
            var needsReschedule = sorteo.CantidadBoletos > 0 &&
                soldCount * 100m < sorteo.CantidadBoletos * sorteo.PorcentajeMinimoVenta;
            return new(sorteoId, sorteo.Fecha, needsReschedule ? "reprogramar" : published ? "publicado" : "pendiente");
        }

        var state = await GenerateIfNeededAsync(sorteoId, cancellationToken);
        return new(sorteoId, sorteo.Fecha, state);
    }

    public async Task<(byte[] Content, string Hash)?> GetPdfAsync(int sorteoId, CancellationToken cancellationToken)
    {
        var status = await GetStatusAsync(sorteoId, cancellationToken);
        if (status?.Estado != "publicado") return null;
        var sorteo = await _context.Sorteos.AsNoTracking()
            .SingleAsync(x => x.Id == sorteoId, cancellationToken);
        if (sorteo.Fecha != status.FechaProgramada) return null;
        var pdf = await _files.ReadAsync(sorteo, cancellationToken);
        if (pdf == null) return null;
        return (pdf, Convert.ToHexString(SHA256.HashData(pdf)));
    }

    private async Task<string> GenerateIfNeededAsync(int sorteoId, CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var resource = $"eLotto:Sorteo:{sorteoId}:Boletos";
        await _context.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = {resource}, @LockMode = 'Exclusive',
                @LockOwner = 'Transaction', @LockTimeout = 30000;
            IF @result < 0
                THROW 51010, 'No se pudo bloquear el sorteo para transparencia.', 1;
            """, cancellationToken);

        var sorteo = await _context.Sorteos
            .FromSqlInterpolated($"SELECT * FROM Sorteos WITH (UPDLOCK, HOLDLOCK) WHERE Id = {sorteoId}")
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        if (sorteo == null) return "no_aplica";
        if (_files.Exists(sorteo)) return "publicado";

        var scheduledAt = _sorteoTimeService.ResolveScheduledTime(sorteo);
        if (ApplicationClock.NowOffset < scheduledAt.AddMinutes(-_publicationMinutes))
            return "pendiente";
        if (!string.IsNullOrWhiteSpace(sorteo.NumeroGanador))
            return "no_aplica";

        var sold = await _context.BoletosConfirmados.AsNoTracking()
            .Where(x => x.SorteosId == sorteoId)
            .Select(x => x.Numero)
            .ToListAsync(cancellationToken);
        string[] unsold;
        try
        {
            unsold = SorteoTransparencyNumbers.GetUnsold(sorteo.CantidadBoletos, sold);
        }
        catch (InvalidOperationException error)
        {
            _logger.LogCritical(error, "La venta del sorteo {SorteoId} no pasó la comprobación de integridad para transparencia.", sorteoId);
            return "incidencia";
        }

        if (sold.Count * 100m < sorteo.CantidadBoletos * sorteo.PorcentajeMinimoVenta)
            return "reprogramar";
        if (unsold.Length == 0)
            return "no_aplica";

        var closedAt = _sorteoTimeService.ConvertToSorteoTime(
            scheduledAt.AddMinutes(-_closeMinutes), sorteo).DateTime;
        var issuedAt = _sorteoTimeService.ConvertToSorteoTime(
            ApplicationClock.NowOffset, sorteo).DateTime;
        var pdf = SorteoTransparencyPdf.Generate(sorteo.Id, sorteo.Nombre, _applicationName, sorteo.Fecha,
            sorteo.ZonaHoraria, closedAt, issuedAt,
            sorteo.CantidadBoletos, sold.Count, unsold);
        await _files.WriteOnceAsync(sorteo, pdf, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return "publicado";
    }
}
