using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Security.Cryptography;
using eLotto.Core.Data;
using eLotto.Core.Models;
using eLotto.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace eLotto.Core.Repository;

public sealed record TicketAvailability(string Numero, bool Disponible);
public sealed record TicketQueryResult(string Folio, IReadOnlyList<TicketAvailability> Tickets);
public sealed record PreReserveResult(bool Ok, string Folio, DateTimeOffset? ExpiresAt, IReadOnlyList<string> Numbers, IReadOnlyList<string> Unavailable);

public interface IUserLotteryRepository
{
    Task<Sorteos> GetCurrentAsync(DateTimeOffset serverNow, CancellationToken cancellationToken);
    Task EnsureTicketsAsync(Sorteos sorteo, CancellationToken cancellationToken);
    Task<int> CountSoldTicketsAsync(int sorteoId, CancellationToken cancellationToken);
    Task<decimal> GetAvailableScratchcardPrizeAmountAsync(int sorteoId, CancellationToken cancellationToken);
    Task<TicketQueryResult> QueryAvailabilityAsync(int sorteoId, int userId, IReadOnlyList<string> numbers, CancellationToken cancellationToken);
    Task<PreReserveResult> PreReserveAsync(int sorteoId, int userId, string folio, IReadOnlyList<string> numbers, CancellationToken cancellationToken);
    Task<PreReserveResult> PreReserveRandomAsync(int sorteoId, int userId, int quantity, string type, string value, CancellationToken cancellationToken);
    Task<TicketPurchaseResult> ConfirmPurchaseAsync(int sorteoId, int userId, string folio, IReadOnlyList<string> numbers, CancellationToken cancellationToken);
    Task ReleaseAsync(int sorteoId, int userId, CancellationToken cancellationToken);
}

public sealed partial class UserLotteryRepository : IUserLotteryRepository
{
    private const int LockTimeoutMilliseconds = 30000;
    private const int RandomAttempts = 3;
    private readonly eLottoContext _context;
    private readonly int _reservationMinutes;
    private readonly int _salesCloseMinutes;
    private readonly ISorteoTimeService _sorteoTimeService;

    public UserLotteryRepository(eLottoContext context) : this(context, null, new SorteoTimeService()) { }

    public UserLotteryRepository(eLottoContext context, IConfiguration configuration, ISorteoTimeService sorteoTimeService)
    {
        _context = context;
        _sorteoTimeService = sorteoTimeService;
        _reservationMinutes = PositiveInt(configuration?["LotteryRules:ReservationMinutes"], 7);
        _salesCloseMinutes = PositiveInt(configuration?["LotteryRules:SalesCloseMinutesBeforeDraw"], SorteoDisponibilidadPolicy.MinutosBloqueoPredeterminados);
    }

    private static int PositiveInt(string value, int fallback) =>
        int.TryParse(value, out var configured) && configured > 0 ? configured : fallback;

    public async Task<Sorteos> GetCurrentAsync(DateTimeOffset serverNow, CancellationToken cancellationToken)
    {
        var candidates = await _context.Sorteos.AsNoTracking()
            .Where(x => x.NumeroGanador == null || x.NumeroGanador == string.Empty)
            .Select(x => new { x.Id, x.Fecha, x.ZonaHoraria })
            .ToListAsync(cancellationToken);

        var scheduled = candidates.Select(x => new
            {
                x.Id,
                DrawAt = _sorteoTimeService.ResolveScheduledTime(new Sorteos
                {
                    Fecha = x.Fecha,
                    ZonaHoraria = x.ZonaHoraria
                })
            })
            .ToArray();
        var currentId = scheduled.Where(x => x.DrawAt <= serverNow)
            .OrderByDescending(x => x.DrawAt)
            .Select(x => (int?)x.Id)
            .FirstOrDefault()
            ?? scheduled.Where(x => x.DrawAt > serverNow)
                .OrderBy(x => x.DrawAt)
                .Select(x => (int?)x.Id)
                .FirstOrDefault();

        return currentId == null ? null : await _context.Sorteos.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == currentId.Value, cancellationToken);
    }

    public Task<int> CountSoldTicketsAsync(int sorteoId, CancellationToken cancellationToken) =>
        _context.BoletosConfirmados.AsNoTracking()
            .CountAsync(x => x.SorteosId == sorteoId, cancellationToken);

    public async Task<decimal> GetAvailableScratchcardPrizeAmountAsync(
        int sorteoId,
        CancellationToken cancellationToken) =>
        await _context.SorteosRascaditoPremios.AsNoTracking()
            .Where(x => x.SorteosId == sorteoId)
            .SumAsync(x => (decimal?)(x.Premio * (x.Cantidad - x.Entregados)), cancellationToken) ?? 0m;

    public async Task EnsureTicketsAsync(Sorteos sorteo, CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var width = sorteo.CantidadBoletos.ToString().Length;
        var iniciaEnUno = sorteo.CantidadBoletos == 60000;
        var numeroInicial = iniciaEnUno ? 1 : 0;

        try
        {
            await AcquireLotteryLockAsync(
                connection,
                transaction,
                sorteo.Id,
                cancellationToken);

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandTimeout = 180;
            command.CommandText = @"
IF NOT EXISTS (SELECT 1 FROM SorteosBoletos WHERE SorteosId = @sorteoId)
   AND NOT EXISTS (SELECT 1 FROM BoletosConfirmados WHERE SorteosId = @sorteoId)
BEGIN
    ;WITH Ten(N) AS
    (
        SELECT N FROM (VALUES (0),(1),(2),(3),(4),(5),(6),(7),(8),(9)) D(N)
    ),
    Numbers AS
    (
        SELECT TOP (@cantidad)
            ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 + @numeroInicial AS Numero
        FROM Ten A
        CROSS JOIN Ten B
        CROSS JOIN Ten C
        CROSS JOIN Ten D
        CROSS JOIN Ten E
        CROSS JOIN Ten F
    )
    INSERT INTO SorteosBoletos
        (SorteosId, Numero, FolioCompra, UsuarioId, Fecha, Pagado, PreApartado, Apartado, Aviso)
    SELECT
        @sorteoId,
        RIGHT(REPLICATE('0', @ancho) + CONVERT(varchar(20), Numero), @ancho),
        '', 0, @serverNow, 0, 0, 0, 0
    FROM Numbers;
END;";
            Add(command, "@sorteoId", sorteo.Id);
            Add(command, "@cantidad", sorteo.CantidadBoletos);
            Add(command, "@numeroInicial", numeroInicial);
            Add(command, "@ancho", width);
            Add(command, "@serverNow", ApplicationClock.Now);
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await RollbackIfNeededAsync(transaction, cancellationToken);
            throw;
        }
    }

    public async Task<TicketQueryResult> QueryAvailabilityAsync(int sorteoId, int userId, IReadOnlyList<string> numbers, CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            await AcquireLotteryLockAsync(connection, transaction, sorteoId, cancellationToken);
            await ReleaseExpiredAsync(connection, transaction, sorteoId, cancellationToken);
            await ReleaseUserAsync(connection, transaction, sorteoId, userId, cancellationToken);
            var folio = await CreateUniqueFolioAsync(connection, transaction, userId, cancellationToken);
            var availability = await ReadAvailabilityAsync(connection, transaction, sorteoId, JsonSerializer.Serialize(numbers), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            var tickets = numbers.Select(number =>
            {
                var storedNumber = availability.GetValueOrDefault(number);
                return new TicketAvailability(storedNumber ?? number, storedNumber is not null);
            }).ToArray();
            return new TicketQueryResult(folio, tickets);
        }
        catch
        {
            await RollbackIfNeededAsync(transaction, cancellationToken);
            throw;
        }
    }

    public async Task<PreReserveResult> PreReserveAsync(int sorteoId, int userId, string folio, IReadOnlyList<string> numbers, CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            await AcquireLotteryLockAsync(connection, transaction, sorteoId, cancellationToken);
            await ReleaseExpiredAsync(connection, transaction, sorteoId, cancellationToken);
            await ReleaseUserAsync(connection, transaction, sorteoId, userId, cancellationToken);
            await EnsureFolioAvailableAsync(connection, transaction, userId, folio, cancellationToken);
            var schedule = await ReadSalesScheduleAsync(connection, transaction, sorteoId, cancellationToken);
            var reservedAt = ApplicationClock.NowOffset;
            var reserved = await ReserveNumbersAsync(connection, transaction, sorteoId, userId, folio, numbers,
                schedule, reservedAt.DateTime, cancellationToken);
            var unavailable = numbers.Except(reserved, StringComparer.Ordinal).ToArray();
            if (unavailable.Length > 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(false, null, null, [], unavailable);
            }
            await transaction.CommitAsync(cancellationToken);
            return new(true, folio, reservedAt.AddMinutes(_reservationMinutes), OrderNumbers(reserved), []);
        }
        catch
        {
            await RollbackIfNeededAsync(transaction, cancellationToken);
            throw;
        }
    }

    public async Task<PreReserveResult> PreReserveRandomAsync(int sorteoId, int userId, int quantity, string type, string value, CancellationToken cancellationToken)
    {
        var pattern = type switch { "inicio" => $"{value}%", "fin" => $"%{value}", "incluya" => $"%{value}%", _ => "%" };
        var connection = _context.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            await AcquireLotteryLockAsync(connection, transaction, sorteoId, cancellationToken);
            await ReleaseExpiredAsync(connection, transaction, sorteoId, cancellationToken);
            await ReleaseUserAsync(connection, transaction, sorteoId, userId, cancellationToken);

            var folio = await CreateUniqueFolioAsync(connection, transaction, userId, cancellationToken);
            var schedule = await ReadSalesScheduleAsync(connection, transaction, sorteoId, cancellationToken);
            var reservedAt = ApplicationClock.NowOffset;

            var reserved = new List<string>(quantity);
            for (var attempt = 0; attempt < RandomAttempts && reserved.Count < quantity; attempt++)
            {
                var missing = quantity - reserved.Count;
                var candidates = await GetRandomCandidatesAsync(connection, transaction, sorteoId, checked(missing * 20), pattern, schedule, cancellationToken);
                if (candidates.Count == 0)
                    break;

                var updated = await ReserveNumbersAsync(connection, transaction, sorteoId, userId, folio,
                    candidates.Take(missing).ToArray(), schedule, reservedAt.DateTime, cancellationToken);
                foreach (var number in updated)
                    if (!reserved.Contains(number, StringComparer.Ordinal))
                        reserved.Add(number);
            }

            if (reserved.Count == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(false, null, null, [], []);
            }
            await transaction.CommitAsync(cancellationToken);
            return new(true, folio, reservedAt.AddMinutes(_reservationMinutes), OrderNumbers(reserved), []);
        }
        catch
        {
            await RollbackIfNeededAsync(transaction, cancellationToken);
            throw;
        }
    }

    public async Task ReleaseAsync(int sorteoId, int userId, CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            await AcquireLotteryLockAsync(connection, transaction, sorteoId, cancellationToken);
            await ReleaseUserAsync(connection, transaction, sorteoId, userId, cancellationToken);
            await ReleaseExpiredAsync(connection, transaction, sorteoId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await RollbackIfNeededAsync(transaction, cancellationToken);
            throw;
        }
    }

    private static async Task AcquireLotteryLockAsync(DbConnection connection, DbTransaction transaction, int sorteoId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
DECLARE @result int;
EXEC @result = sys.sp_getapplock
    @Resource = @resource,
    @LockMode = 'Exclusive',
    @LockOwner = 'Transaction',
    @LockTimeout = @lockTimeout;
IF @result < 0
    THROW 51000, 'No fue posible obtener el bloqueo para seleccionar boletos.', 1;";
        Add(command, "@resource", $"eLotto:Sorteo:{sorteoId}:Boletos");
        Add(command, "@lockTimeout", LockTimeoutMilliseconds);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<Dictionary<string, string>> ReadAvailabilityAsync(DbConnection connection, DbTransaction transaction, int sorteoId, string numbersJson, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
WITH requested AS
(
    SELECT Numero, TRY_CONVERT(bigint, Numero) AS NumeroNumerico
    FROM OPENJSON(@numbers) WITH (Numero nvarchar(32) '$')
)
SELECT requested.Numero,
       available.Numero
FROM requested
OUTER APPLY
(
    SELECT TOP (1) b.Numero
    FROM SorteosBoletos b WITH (HOLDLOCK)
    WHERE b.SorteosId = @sorteoId
      AND TRY_CONVERT(bigint, b.Numero) = requested.NumeroNumerico
      AND b.Pagado = 0
      AND b.PreApartado = 0
      AND b.Apartado = 0
) available;";
        Add(command, "@numbers", numbersJson);
        Add(command, "@sorteoId", sorteoId);

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            if (!reader.IsDBNull(1))
                result[reader.GetString(0)] = reader.GetString(1);
        return result;
    }

    private async Task<List<string>> GetRandomCandidatesAsync(DbConnection connection, DbTransaction transaction, int sorteoId, int candidateCount, string pattern, SalesSchedule schedule, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
SELECT TOP (@candidateCount) Numero
FROM SorteosBoletos WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
WHERE SorteosId = @sorteoId
  AND PreApartado = 0 AND Apartado = 0 AND Pagado = 0
  AND EXISTS
  (
      SELECT 1
      FROM Sorteos s WITH (HOLDLOCK)
      WHERE s.Id = SorteosBoletos.SorteosId
        AND s.Fecha = @scheduledDate AND s.ZonaHoraria = @scheduledZone
        AND @serverNowOffset < @salesCloseAt
        AND (s.NumeroGanador IS NULL OR LTRIM(RTRIM(s.NumeroGanador)) = '')
  )
  AND Numero LIKE @pattern
ORDER BY NEWID();";
        Add(command, "@candidateCount", candidateCount);
        Add(command, "@sorteoId", sorteoId);
        Add(command, "@pattern", pattern);
        AddSalesGuard(command, schedule);
        return await ReadStringsAsync(command, cancellationToken);
    }

    private Task<List<string>> ReserveNumbersAsync(DbConnection connection, DbTransaction transaction, int sorteoId, int userId,
        string folio, IReadOnlyList<string> numbers, SalesSchedule schedule, DateTime reservedAt,
        CancellationToken cancellationToken) =>
        ExecuteNumbersAsync(connection, transaction, @"
UPDATE b WITH (ROWLOCK)
SET Fecha = @serverNow, PreApartado = 1, FolioCompra = @folio, UsuarioId = @userId
OUTPUT INSERTED.Numero
FROM SorteosBoletos b
INNER JOIN OPENJSON(@numbers) WITH (Numero nvarchar(32) '$') requested ON requested.Numero = b.Numero
WHERE b.SorteosId = @sorteoId
  AND b.PreApartado = 0 AND b.Apartado = 0 AND b.Pagado = 0
  AND EXISTS
  (
      SELECT 1
      FROM Sorteos s WITH (HOLDLOCK)
      WHERE s.Id = b.SorteosId
        AND s.Fecha = @scheduledDate AND s.ZonaHoraria = @scheduledZone
        AND @serverNowOffset < @salesCloseAt
        AND (s.NumeroGanador IS NULL OR LTRIM(RTRIM(s.NumeroGanador)) = '')
  );",
            sorteoId, userId, folio, JsonSerializer.Serialize(numbers), schedule, reservedAt, cancellationToken);

    private async Task ReleaseExpiredAsync(DbConnection connection, DbTransaction transaction, int sortoId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
UPDATE SorteosBoletos
SET PreApartado = 0, FolioCompra = '', UsuarioId = 0
WHERE SorteosId = @sorteoId
  AND PreApartado = 1 AND Apartado = 0 AND Pagado = 0
  AND Fecha <= DATEADD(MINUTE, -@reservationMinutes, @serverNow);";
        Add(command, "@sorteoId", sortoId);
        Add(command, "@reservationMinutes", _reservationMinutes);
        Add(command, "@serverNow", ApplicationClock.Now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Task ReleaseUserAsync(DbConnection connection, DbTransaction transaction, int sorteoId, int userId, CancellationToken cancellationToken) =>
        ExecuteAsync(connection, transaction, @"
UPDATE SorteosBoletos
SET PreApartado = 0, FolioCompra = '', UsuarioId = 0
WHERE SorteosId = @sorteoId
  AND UsuarioId = @userId
  AND PreApartado = 1 AND Apartado = 0 AND Pagado = 0;", sorteoId, userId, cancellationToken);

    private static async Task ExecuteAsync(DbConnection connection, DbTransaction transaction, string sql, int sorteoId, int userId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        Add(command, "@sorteoId", sorteoId);
        if (sql.Contains("@userId", StringComparison.Ordinal))
            Add(command, "@userId", userId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<List<string>> ExecuteNumbersAsync(DbConnection connection, DbTransaction transaction, string sql, int sorteoId,
        int userId, string folio, string json, SalesSchedule schedule, DateTime reservedAt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        Add(command, "@sorteoId", sorteoId);
        Add(command, "@userId", userId);
        Add(command, "@folio", folio);
        Add(command, "@numbers", json);
        AddSalesGuard(command, schedule);
        Add(command, "@serverNow", reservedAt);
        return await ReadStringsAsync(command, cancellationToken);
    }

    private async Task<SalesSchedule> ReadSalesScheduleAsync(DbConnection connection, DbTransaction transaction,
        int sorteoId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Fecha, ZonaHoraria FROM Sorteos WITH (UPDLOCK, HOLDLOCK) WHERE Id = @sorteoId;";
        Add(command, "@sorteoId", sorteoId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("El sorteo ya no está disponible.");

        var sorteo = new Sorteos { Fecha = reader.GetDateTime(0), ZonaHoraria = reader.GetString(1) };
        return new SalesSchedule(sorteo.Fecha, sorteo.ZonaHoraria,
            SorteoDisponibilidadPolicy.ObtenerInicioBloqueo(sorteo, _sorteoTimeService, _salesCloseMinutes));
    }

    private static void AddSalesGuard(DbCommand command, SalesSchedule schedule)
    {
        Add(command, "@scheduledDate", schedule.ScheduledDate);
        Add(command, "@scheduledZone", schedule.ZoneId);
        Add(command, "@salesCloseAt", schedule.SalesCloseAt);
        Add(command, "@serverNowOffset", ApplicationClock.NowOffset);
    }

    private sealed record SalesSchedule(DateTime ScheduledDate, string ZoneId, DateTimeOffset SalesCloseAt);

    private static async Task<List<string>> ReadStringsAsync(DbCommand command, CancellationToken cancellationToken)
    {
        var values = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            values.Add(reader.GetString(0));
        return values;
    }

    private static async Task EnsureOpenAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);
    }

    private static async Task RollbackIfNeededAsync(DbTransaction transaction, CancellationToken cancellationToken)
    {
        if (transaction.Connection is not null)
            await transaction.RollbackAsync(cancellationToken);
    }

    private static IReadOnlyList<string> OrderNumbers(IEnumerable<string> numbers) =>
        numbers.OrderBy(number => number, StringComparer.Ordinal).ToArray();

    private static void Add(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        if (value is DateTime) parameter.DbType = DbType.DateTime2;
        if (value is DateTimeOffset) parameter.DbType = DbType.DateTimeOffset;
        command.Parameters.Add(parameter);
    }

    private const string FolioCharacters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int FolioLength = 8;

    private static async Task<string> CreateUniqueFolioAsync(
        DbConnection connection,
        DbTransaction transaction,
        int userId,
        CancellationToken cancellationToken)
    {
        await AcquireUserFolioLockAsync(connection, transaction, userId, cancellationToken);

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var folio = CreateRandomFolio();
            if (!await FolioExistsAsync(connection, transaction, userId, folio, cancellationToken))
                return folio;
        }

        throw new InvalidOperationException("No fue posible generar un folio de compra único.");
    }

    private static async Task EnsureFolioAvailableAsync(
        DbConnection connection,
        DbTransaction transaction,
        int userId,
        string folio,
        CancellationToken cancellationToken)
    {
        await AcquireUserFolioLockAsync(connection, transaction, userId, cancellationToken);
        if (await FolioExistsAsync(connection, transaction, userId, folio, cancellationToken))
            throw new InvalidOperationException("El folio de compra ya fue utilizado. Consulta nuevamente tus boletos.");
    }

    private static async Task AcquireUserFolioLockAsync(
        DbConnection connection,
        DbTransaction transaction,
        int userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
DECLARE @result int;
EXEC @result = sys.sp_getapplock
    @Resource = @resource,
    @LockMode = 'Exclusive',
    @LockOwner = 'Transaction',
    @LockTimeout = @lockTimeout;
IF @result < 0
    THROW 51001, 'No fue posible generar el folio de compra.', 1;";
        Add(command, "@resource", $"eLotto:Usuario:{userId}:Folios");
        Add(command, "@lockTimeout", LockTimeoutMilliseconds);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<bool> FolioExistsAsync(
        DbConnection connection,
        DbTransaction transaction,
        int userId,
        string folio,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
SELECT CONVERT(bit, CASE WHEN
    EXISTS (SELECT 1 FROM SorteosBoletos WHERE UsuarioId = @userId AND FolioCompra = @folio)
    OR EXISTS (SELECT 1 FROM BoletosConfirmados WHERE UsuarioId = @userId AND FolioCompra = @folio)
THEN 1 ELSE 0 END);";
        Add(command, "@userId", userId);
        Add(command, "@folio", folio);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static string CreateRandomFolio()
    {
        var folio = new char[FolioLength];
        for (var index = 0; index < folio.Length; index++)
            folio[index] = FolioCharacters[RandomNumberGenerator.GetInt32(FolioCharacters.Length)];
        return new string(folio);
    }
}
