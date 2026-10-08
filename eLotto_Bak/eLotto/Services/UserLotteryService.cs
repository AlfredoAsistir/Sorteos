using eLotto.Core.Models;
using eLotto.Core.Repository;
using eLotto.Core.Services;
using eLotto.Models;
using eLotto.Options;
using Microsoft.Extensions.Options;

namespace eLotto.Services;

public interface IUserLotteryService
{
    Task<CurrentLotteryDto> GetCurrentAsync(CancellationToken cancellationToken);
    Task<ManualTicketQueryResponseDto> QueryManualAsync(int userId, ManualTicketQueryDto request, CancellationToken cancellationToken);
    Task<PreReserveDto> PreReserveManualAsync(int userId, ManualPreReserveDto request, CancellationToken cancellationToken);
    Task<PreReserveDto> PreReserveRandomAsync(int userId, RandomPreReserveDto request, CancellationToken cancellationToken);
    Task<TicketPurchaseDto> ConfirmPurchaseAsync(int userId, ConfirmTicketPurchaseDto request, CancellationToken cancellationToken);
    Task<CurrentUserTicketsDto> GetMyTicketsAsync(int userId, CancellationToken cancellationToken);
    Task<TicketWhatsAppResendDto> ResendTicketsAsync(int userId, string folio, CancellationToken cancellationToken);
    Task ReleaseAsync(int userId, CancellationToken cancellationToken);
}

public sealed class UserLotteryService : IUserLotteryService
{
    private readonly IUserLotteryRepository _repository;
    private readonly IConfirmedTicketPurchaseRepository _confirmedPurchaseRepository;
    private readonly IConfirmedTicketDeliveryService _confirmedTicketDeliveryService;
    private readonly IWhatsAppResendThrottle _whatsAppResendThrottle;
    private readonly ILogger<UserLotteryService> _logger;
    private readonly ISorteoTimeService _sorteoTimeService;
    private readonly int _salesCloseMinutes;

    public UserLotteryService(
        IUserLotteryRepository repository,
        IConfirmedTicketPurchaseRepository confirmedPurchaseRepository,
        IConfirmedTicketDeliveryService confirmedTicketDeliveryService,
        IWhatsAppResendThrottle whatsAppResendThrottle,
        ILogger<UserLotteryService> logger,
        IOptions<LotteryRulesOptions> lotteryRulesOptions,
        ISorteoTimeService sorteoTimeService)
    {
        _repository = repository;
        _confirmedPurchaseRepository = confirmedPurchaseRepository;
        _confirmedTicketDeliveryService = confirmedTicketDeliveryService;
        _whatsAppResendThrottle = whatsAppResendThrottle;
        _logger = logger;
        _sorteoTimeService = sorteoTimeService;
        _salesCloseMinutes = Math.Max(1, lotteryRulesOptions.Value.SalesCloseMinutesBeforeDraw);
    }

    public async Task<CurrentLotteryDto> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var ahora = ApplicationClock.NowOffset;
        var sorteo = await GetCurrentEntityAsync(ahora, cancellationToken);
        if (sorteo == null) return null;

        var soldTickets = await _repository.CountSoldTicketsAsync(sorteo.Id, cancellationToken);
        var availableScratchcardPrizeAmount = sorteo.RascaditosHabilitados
            ? await _repository.GetAvailableScratchcardPrizeAmountAsync(sorteo.Id, cancellationToken)
            : 0m;
        var salesPercentage = sorteo.CantidadBoletos <= 0
            ? 0m
            : Math.Min(100m, Math.Round(soldTickets * 100m / sorteo.CantidadBoletos, 1));
        var drawAt = _sorteoTimeService.ResolveScheduledTime(sorteo);
        var salesCloseAt = SorteoDisponibilidadPolicy.ObtenerInicioBloqueo(sorteo, _sorteoTimeService, _salesCloseMinutes);
        var segundosParaInicio = Math.Clamp(
            (long)Math.Ceiling(Math.Max(0, (drawAt - ahora).TotalSeconds)),
            0,
            int.MaxValue);
        var segundosParaCierre = Math.Clamp(
            (long)Math.Ceiling(Math.Max(0, (salesCloseAt - ahora).TotalSeconds)),
            0,
            int.MaxValue);

        return new(
            sorteo.Id,
            sorteo.Nombre,
            sorteo.Fecha,
            sorteo.ZonaHoraria,
            _sorteoTimeService.ConvertToSorteoTime(salesCloseAt, sorteo).DateTime,
            SorteoDisponibilidadPolicy.ObtenerEstado(sorteo, ahora, _sorteoTimeService, _salesCloseMinutes),
            SorteoDisponibilidadPolicy.VentaDisponible(sorteo, ahora, _sorteoTimeService, _salesCloseMinutes),
            (int)segundosParaInicio,
            (int)segundosParaCierre,
            sorteo.UrlTransmisionEnVivo,
            sorteo.PrecioBoleto,
            sorteo.PrecioPorMil,
            sorteo.CantidadBoletos,
            soldTickets,
            salesPercentage,
            sorteo.PorcentajeMinimoVenta,
            sorteo.CantidadBoletos > 0 &&
                soldTickets * 100m >= sorteo.CantidadBoletos * sorteo.PorcentajeMinimoVenta,
            sorteo.RascaditosHabilitados,
            availableScratchcardPrizeAmount,
            sorteo.Imagen1,
            sorteo.Imagen2,
            sorteo.Imagen3,
            sorteo.Imagen2Tema,
            sorteo.Imagen3Tema);
    }

    public async Task<CurrentUserTicketsDto> GetMyTicketsAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        var sorteo = await GetCurrentEntityAsync(cancellationToken);
        if (sorteo == null)
            return null;

        var purchases = await _confirmedPurchaseRepository.ListAsync(
            sorteo.Id,
            userId,
            cancellationToken);

        return new(
            sorteo.Id,
            sorteo.Nombre,
            sorteo.Fecha,
            purchases.Select(purchase => new UserTicketPurchaseDto(
                purchase.FolioCompra,
                purchase.FechaCompra,
                purchase.CantidadBoletos,
                purchase.PrecioBoleto,
                purchase.Importe,
                purchase.Numeros)).ToArray(),
            _whatsAppResendThrottle.GetRemainingSeconds(userId));
    }

    public async Task<TicketWhatsAppResendDto> ResendTicketsAsync(
        int userId,
        string folio,
        CancellationToken cancellationToken)
    {
        var sorteo = await RequireCurrentAsync(cancellationToken);
        var normalizedFolio = (folio ?? string.Empty).Trim().ToUpperInvariant();
        const string allowedFolioCharacters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        if (normalizedFolio.Length != 8
            || normalizedFolio.Any(character => !allowedFolioCharacters.Contains(character)))
            throw new InvalidOperationException("El folio de compra no es válido.");

        _ = await _confirmedPurchaseRepository.GetAsync(
                sorteo.Id,
                userId,
                normalizedFolio,
                cancellationToken)
            ?? throw new InvalidOperationException("No se encontró la compra confirmada en el sorteo vigente.");

        var throttle = _whatsAppResendThrottle.TryAcquire(userId);
        if (!throttle.Acquired)
        {
            return new(
                false,
                $"Espera {throttle.RemainingSeconds} segundos antes de solicitar otro envío.",
                throttle.RemainingSeconds);
        }

        await _confirmedTicketDeliveryService.DeliverAsync(
            sorteo.Id,
            userId,
            normalizedFolio,
            cancellationToken);

        _logger.LogInformation(
            "El usuario solicitó el reenvío del comprobante para SorteoId {SorteoId}, FolioCompra {FolioCompra}, UsuarioId {UsuarioId}.",
            sorteo.Id,
            normalizedFolio,
            userId);

        return new(
            true,
            "El comprobante fue enviado nuevamente por WhatsApp.",
            _whatsAppResendThrottle.GetRemainingSeconds(userId));
    }

    public async Task<ManualTicketQueryResponseDto> QueryManualAsync(int userId, ManualTicketQueryDto request, CancellationToken cancellationToken)
    {
        var sorteo = await RequireSalesOpenAsync(cancellationToken);
        var numbers = ParseNumbers(request.Numeros, sorteo);
        var result = await _repository.QueryAvailabilityAsync(sorteo.Id, userId, numbers, cancellationToken);
        return new(result.Folio, result.Tickets.Select(x => new TicketAvailabilityDto(x.Numero, x.Disponible)).ToArray());
    }

    public async Task<PreReserveDto> PreReserveManualAsync(int userId, ManualPreReserveDto request, CancellationToken cancellationToken)
    {
        var sorteo = await RequireSalesOpenAsync(cancellationToken);
        var width = sorteo.CantidadBoletos.ToString().Length;
        var numbers = request.Numeros.Select(x => Normalize(x, width)).Distinct().ToArray();
        if (numbers.Length is 0 or > 2000) throw new InvalidOperationException("Selecciona entre 1 y 2000 boletos.");
        var folio = request.FolioCompra.Trim().ToUpperInvariant();
        const string allowedFolioCharacters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        if (folio.Length != 8 || folio.Any(character => !allowedFolioCharacters.Contains(character)))
            throw new InvalidOperationException("El folio de compra no es válido. Consulta nuevamente tus boletos.");
        return Map(await _repository.PreReserveAsync(sorteo.Id, userId, folio, numbers, cancellationToken));
    }

    public async Task<PreReserveDto> PreReserveRandomAsync(int userId, RandomPreReserveDto request, CancellationToken cancellationToken)
    {
        var sorteo = await RequireSalesOpenAsync(cancellationToken);
        var type = request.Tipo.Trim().ToLowerInvariant();
        if (type is not ("azar" or "inicio" or "fin" or "incluya")) throw new InvalidOperationException("Tipo de asignación inválido.");
        var value = new string((request.Valor ?? string.Empty).Where(char.IsDigit).ToArray());
        if (type != "azar" && string.IsNullOrEmpty(value)) throw new InvalidOperationException("Captura el número que deben contener los boletos.");
        return Map(await _repository.PreReserveRandomAsync(sorteo.Id, userId, request.Cantidad, type, value, cancellationToken));
    }

    public async Task<TicketPurchaseDto> ConfirmPurchaseAsync(
        int userId,
        ConfirmTicketPurchaseDto request,
        CancellationToken cancellationToken)
    {
        var sorteo = await RequireSalesOpenAsync(cancellationToken);
        if (request.SorteoId != sorteo.Id)
            throw new InvalidOperationException("El sorteo cambió. Consulta nuevamente tus boletos.");

        var width = sorteo.CantidadBoletos.ToString().Length;
        var numbers = request.Numeros
            .Select(number => Normalize(number, width))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (numbers.Length is 0 or > 2000)
            throw new InvalidOperationException("Selecciona entre 1 y 2000 boletos.");

        var folio = request.FolioCompra.Trim().ToUpperInvariant();
        const string allowedFolioCharacters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        if (folio.Length != 8 || folio.Any(character => !allowedFolioCharacters.Contains(character)))
            throw new InvalidOperationException("El folio de compra no es válido. Consulta nuevamente tus boletos.");

        var result = await _repository.ConfirmPurchaseAsync(
            sorteo.Id, userId, folio, numbers, cancellationToken);

        if (result.Ok)
        {
            try
            {
                await _confirmedTicketDeliveryService.DeliverAsync(
                    sorteo.Id,
                    userId,
                    folio,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "La compra quedó confirmada, pero falló la entrega del comprobante para SorteoId {SorteoId}, FolioCompra {FolioCompra}, UsuarioId {UsuarioId}.",
                    sorteo.Id,
                    folio,
                    userId);
            }
        }

        var message = result.Code switch
        {
            "insufficient_balance" => "Saldo insuficiente para completar la compra.",
            "tickets_unavailable" => "Algunos boletos ya no están disponibles.",
            _ => "Compra realizada. ¡Mucha suerte!"
        };
        return new(
            result.Ok,
            result.Code,
            message,
            result.Total,
            result.Balance,
            result.Numbers,
            result.Unavailable);
    }
    public async Task ReleaseAsync(int userId, CancellationToken cancellationToken)
    {
        var sorteo = await GetCurrentEntityAsync(cancellationToken);
        if (sorteo != null) await _repository.ReleaseAsync(sorteo.Id, userId, cancellationToken);
    }

    private Task<Sorteos> GetCurrentEntityAsync(CancellationToken cancellationToken) =>
        GetCurrentEntityAsync(ApplicationClock.NowOffset, cancellationToken);

    private async Task<Sorteos> GetCurrentEntityAsync(
        DateTimeOffset ahora,
        CancellationToken cancellationToken)
    {
        var sorteo = await _repository.GetCurrentAsync(ahora, cancellationToken);
        if (sorteo != null && SorteoDisponibilidadPolicy.VentaDisponible(sorteo, ahora, _sorteoTimeService, _salesCloseMinutes))
            await _repository.EnsureTicketsAsync(sorteo, cancellationToken);
        return sorteo;
    }

    private async Task<Sorteos> RequireCurrentAsync(CancellationToken cancellationToken) =>
        await GetCurrentEntityAsync(cancellationToken) ?? throw new InvalidOperationException("No existe un sorteo vigente.");

    private async Task<Sorteos> RequireSalesOpenAsync(CancellationToken cancellationToken)
    {
        var ahora = ApplicationClock.NowOffset;
        var sorteo = await GetCurrentEntityAsync(ahora, cancellationToken)
            ?? throw new InvalidOperationException("No existe un sorteo vigente.");
        if (!SorteoDisponibilidadPolicy.VentaDisponible(sorteo, ahora, _sorteoTimeService, _salesCloseMinutes))
            throw new InvalidOperationException(
                "El sorteo ya no está disponible para compras porque está próximo a iniciar o se encuentra en proceso.");
        return sorteo;
    }

    private static IReadOnlyList<string> ParseNumbers(string input, Sorteos sorteo)
    {
        var clean = string.Concat(input.Where(c => !char.IsWhiteSpace(c))).TrimEnd(',');
        if (string.IsNullOrEmpty(clean)) throw new InvalidOperationException("Captura uno o más números.");
        var width = sorteo.CantidadBoletos.ToString().Length;
        var values = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in clean.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var limits = part.Split('-');
            if (limits.Length == 1) values.Add(Normalize(limits[0], width));
            else if (limits.Length == 2)
            {
                if (!int.TryParse(limits[0], out var start) || !int.TryParse(limits[1], out var end) || start >= end) throw new InvalidOperationException($"El rango {part} es incorrecto; el inicio debe ser menor al final.");
                if (values.Count + (long)end - start + 1 > 2000) throw new InvalidOperationException("No puedes consultar más de 2000 números en total.");
                for (long number = start; number <= end; number++) values.Add(number.ToString().PadLeft(width, '0'));
            }
            else throw new InvalidOperationException("Formato inválido. Ejemplo: 125,185,300-350,400-450.");
            if (values.Count > 2000) throw new InvalidOperationException("No puedes consultar más de 2000 números en total.");
        }
        return values.OrderBy(x => int.Parse(x)).ToArray();
    }

    private static string Normalize(string value, int width)
    {
        if (!int.TryParse(value, out var number) || number < 0) throw new InvalidOperationException($"El número {value} no es válido.");
        return number.ToString().PadLeft(width, '0');
    }

    private static PreReserveDto Map(PreReserveResult result) => result.Ok
        ? new(true, result.Folio, result.ExpiresAt, result.Numbers, [], "Boletos preapartados correctamente.",
            Math.Max(0, (int)Math.Ceiling((result.ExpiresAt!.Value - ApplicationClock.NowOffset).TotalSeconds)))
        : new(false, null, null, [], result.Unavailable, result.Unavailable.Count > 0 ? "Algunos boletos dejaron de estar disponibles." : "No se encontraron boletos disponibles.", 0);
}
