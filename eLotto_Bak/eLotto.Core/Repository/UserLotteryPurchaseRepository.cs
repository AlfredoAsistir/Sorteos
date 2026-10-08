using System.Data;
using System.Data.Common;
using System.Text.Json;
using eLotto.Core.Models;
using eLotto.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace eLotto.Core.Repository;

public sealed record TicketPurchaseResult(
    bool Ok,
    string Code,
    decimal Total,
    decimal Balance,
    IReadOnlyList<string> Numbers,
    IReadOnlyList<string> Unavailable);

public sealed partial class UserLotteryRepository
{
    public async Task<TicketPurchaseResult> ConfirmPurchaseAsync(
        int sorteoId,
        int userId,
        string folio,
        IReadOnlyList<string> numbers,
        CancellationToken cancellationToken)
    {
        var connection = _context.Database.GetDbConnection();
        await EnsureOpenAsync(connection, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            await AcquireLotteryLockAsync(connection, transaction, sorteoId, cancellationToken);
            await ReleaseExpiredAsync(connection, transaction, sorteoId, cancellationToken);
            var schedule = await ReadSalesScheduleAsync(connection, transaction, sorteoId, cancellationToken);

            var numbersJson = JsonSerializer.Serialize(numbers);
            var purchasable = await ReadPurchasableNumbersAsync(
                connection, transaction, sorteoId, userId, folio, numbersJson, cancellationToken);
            var unavailable = numbers.Except(purchasable, StringComparer.Ordinal).Take(3).ToArray();
            if (purchasable.Count != numbers.Count)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(false, "tickets_unavailable", 0m, 0m, [], unavailable);
            }

            var lottery = await ReadLotteryPriceAsync(connection, transaction, sorteoId, schedule, cancellationToken);
            var unitPrice = numbers.Count >= 1000 ? lottery.BulkPrice : lottery.TicketPrice;
            var total = unitPrice * numbers.Count;
            var wallet = await ReadWalletAsync(connection, transaction, userId, cancellationToken);
            if (wallet is null || wallet.Balance < total)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new(false, "insufficient_balance", total, wallet?.Balance ?? 0m, [], []);
            }

            var whatsApp = await ReadBuyerWhatsAppAsync(connection, transaction, userId, cancellationToken);
            var purchaseMoment = ApplicationClock.NowOffset;
            var purchaseAt = _sorteoTimeService.ConvertToSorteoTime(purchaseMoment,
                new Sorteos { ZonaHoraria = schedule.ZoneId }).DateTime;
            var confirmed = await InsertConfirmedTicketsAsync(
                connection, transaction, sorteoId, userId, folio, whatsApp, numbersJson,
                schedule, purchaseAt, cancellationToken);
            if (confirmed.Count != numbers.Count)
                throw new InvalidOperationException("No fue posible confirmar todos los boletos seleccionados.");

            var deleted = await DeletePurchasedTicketsAsync(
                connection, transaction, sorteoId, userId, folio, numbersJson, cancellationToken);
            if (deleted != numbers.Count)
                throw new InvalidOperationException("No fue posible completar el movimiento de los boletos.");

            var newBalance = wallet.Balance - total;
            await DebitWalletAsync(connection, transaction, wallet.Id, total, cancellationToken);
            await InsertPurchaseTransactionAsync(
                connection,
                transaction,
                userId,
                wallet.Id,
                sorteoId,
                total,
                $"Compra de {numbers.Count} boletos - {lottery.Name}",
                purchaseAt,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new(true, "purchase_completed", total, newBalance, OrderNumbers(confirmed), []);
        }
        catch
        {
            await RollbackIfNeededAsync(transaction, cancellationToken);
            throw;
        }
    }

    private async Task<List<string>> ReadPurchasableNumbersAsync(
        DbConnection connection,
        DbTransaction transaction,
        int sorteoId,
        int userId,
        string folio,
        string numbersJson,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
WITH requested AS
(
    SELECT Numero
    FROM OPENJSON(@numbers) WITH (Numero nvarchar(32) '$')
)
SELECT b.Numero
FROM SorteosBoletos b WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
INNER JOIN requested ON requested.Numero = b.Numero
WHERE b.SorteosId = @sorteoId
  AND b.UsuarioId = @userId
  AND b.FolioCompra = @folio
  AND b.PreApartado = 1 AND b.Apartado = 0 AND b.Pagado = 0
  AND b.Fecha > DATEADD(MINUTE, -@reservationMinutes, @serverNow)
  AND NOT EXISTS
  (
      SELECT 1
      FROM BoletosConfirmados confirmed WITH (UPDLOCK, HOLDLOCK)
      WHERE confirmed.SorteosId = b.SorteosId AND confirmed.Numero = b.Numero
  );";
        Add(command, "@numbers", numbersJson);
        Add(command, "@sorteoId", sorteoId);
        Add(command, "@userId", userId);
        Add(command, "@folio", folio);
        Add(command, "@reservationMinutes", _reservationMinutes);
        Add(command, "@serverNow", ApplicationClock.Now);
        return await ReadStringsAsync(command, cancellationToken);
    }

    private async Task<LotteryPrice> ReadLotteryPriceAsync(
        DbConnection connection,
        DbTransaction transaction,
        int sorteoId,
        SalesSchedule schedule,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
SELECT Nombre, PrecioBoleto, PrecioPorMil
FROM Sorteos WITH (UPDLOCK, HOLDLOCK)
WHERE Id = @sorteoId
  AND Fecha = @scheduledDate AND ZonaHoraria = @scheduledZone
  AND @serverNowOffset < @salesCloseAt
  AND (NumeroGanador IS NULL OR LTRIM(RTRIM(NumeroGanador)) = '');";
        Add(command, "@sorteoId", sorteoId);
        AddSalesGuard(command, schedule);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException(
                "El sorteo ya no está disponible para compras porque está próximo a iniciar o se encuentra en proceso.");

        return new(reader.GetString(0), reader.GetDecimal(1), reader.GetDecimal(2));
    }

    private static async Task<WalletBalance> ReadWalletAsync(
        DbConnection connection,
        DbTransaction transaction,
        int userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
SELECT Id, Balance
FROM UserWallets WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
WHERE UserId = @userId;";
        Add(command, "@userId", userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new WalletBalance(reader.GetInt32(0), reader.GetDecimal(1))
            : null;
    }

    private static async Task<string> ReadBuyerWhatsAppAsync(
        DbConnection connection,
        DbTransaction transaction,
        int userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT WhatsApp FROM Users WITH (HOLDLOCK) WHERE Id = @userId;";
        Add(command, "@userId", userId);
        var value = (await command.ExecuteScalarAsync(cancellationToken))?.ToString();
        var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length < 10)
            throw new InvalidOperationException("El usuario no tiene un WhatsApp válido para confirmar la compra.");

        return $"+{digits}";
    }

    private async Task<List<string>> InsertConfirmedTicketsAsync(
        DbConnection connection,
        DbTransaction transaction,
        int sorteoId,
        int userId,
        string folio,
        string whatsApp,
        string numbersJson,
        SalesSchedule schedule,
        DateTime purchaseAt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
INSERT INTO BoletosConfirmados
    (SorteosId, Numero, FolioCompra, UsuarioId, Fecha, UsuarioIdConfirm, WhatsAppConfirm, CuentaAsignada)
OUTPUT INSERTED.Numero
SELECT b.SorteosId, b.Numero, b.FolioCompra, b.UsuarioId, @purchaseAt, @userId, @whatsApp, ''
FROM SorteosBoletos b WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
INNER JOIN OPENJSON(@numbers) WITH (Numero nvarchar(32) '$') requested ON requested.Numero = b.Numero
WHERE b.SorteosId = @sorteoId
  AND b.UsuarioId = @userId
  AND b.FolioCompra = @folio
  AND b.PreApartado = 1 AND b.Apartado = 0 AND b.Pagado = 0
  AND b.Fecha > DATEADD(MINUTE, -@reservationMinutes, @serverNow)
  AND EXISTS (SELECT 1 FROM Sorteos s WITH (HOLDLOCK)
              WHERE s.Id = b.SorteosId
                AND s.Fecha = @scheduledDate AND s.ZonaHoraria = @scheduledZone
                AND @serverNowOffset < @salesCloseAt
                AND (s.NumeroGanador IS NULL OR LTRIM(RTRIM(s.NumeroGanador)) = ''))
  AND NOT EXISTS
  (
      SELECT 1
      FROM BoletosConfirmados confirmed WITH (UPDLOCK, HOLDLOCK)
      WHERE confirmed.SorteosId = b.SorteosId AND confirmed.Numero = b.Numero
  );";
        Add(command, "@numbers", numbersJson);
        Add(command, "@sorteoId", sorteoId);
        Add(command, "@userId", userId);
        Add(command, "@folio", folio);
        Add(command, "@whatsApp", whatsApp);
        Add(command, "@reservationMinutes", _reservationMinutes);
        AddSalesGuard(command, schedule);
        Add(command, "@serverNow", ApplicationClock.Now);
        Add(command, "@purchaseAt", purchaseAt);
        return await ReadStringsAsync(command, cancellationToken);
    }

    private static async Task<int> DeletePurchasedTicketsAsync(
        DbConnection connection,
        DbTransaction transaction,
        int sorteoId,
        int userId,
        string folio,
        string numbersJson,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
DELETE b
FROM SorteosBoletos b
INNER JOIN OPENJSON(@numbers) WITH (Numero nvarchar(32) '$') requested ON requested.Numero = b.Numero
WHERE b.SorteosId = @sorteoId AND b.UsuarioId = @userId AND b.FolioCompra = @folio;";
        Add(command, "@numbers", numbersJson);
        Add(command, "@sorteoId", sorteoId);
        Add(command, "@userId", userId);
        Add(command, "@folio", folio);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task DebitWalletAsync(
        DbConnection connection,
        DbTransaction transaction,
        int walletId,
        decimal total,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
UPDATE UserWallets WITH (ROWLOCK)
SET Balance = Balance - @total, UpdatedAt = @serverNow
WHERE Id = @walletId AND Balance >= @total;";
        Add(command, "@walletId", walletId);
        Add(command, "@total", total);
        Add(command, "@serverNow", ApplicationClock.Now);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("El saldo cambió antes de completar la compra.");
    }

    private static async Task InsertPurchaseTransactionAsync(
        DbConnection connection,
        DbTransaction transaction,
        int userId,
        int walletId,
        int sorteoId,
        decimal total,
        string description,
        DateTime purchaseAt,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
INSERT INTO WalletTransactions
    (UserId, WalletId, SorteoId, Type, Amount, Status, StripePaymentIntentId, StripeEventId,
     Description, StripeMessage, UserMessage, CreatedAt, CompletedAt)
VALUES
    (@userId, @walletId, @sorteoId, 2, @total, 2, NULL, NULL, @description, NULL, NULL,
     @purchaseAt,
     @purchaseAt);";
        Add(command, "@userId", userId);
        Add(command, "@walletId", walletId);
        Add(command, "@sorteoId", sorteoId);
        Add(command, "@total", total);
        Add(command, "@description", description.Length <= 500 ? description : description[..500]);
        Add(command, "@purchaseAt", purchaseAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record LotteryPrice(string Name, decimal TicketPrice, decimal BulkPrice);
    private sealed record WalletBalance(int Id, decimal Balance);
}
