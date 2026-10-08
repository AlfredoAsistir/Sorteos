using System.Globalization;
using eLotto.Core.Data;
using eLotto.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace eLotto.Core.Repository;

public sealed record ConfirmedTicketPurchaseSummary(
    string FolioCompra,
    DateTime FechaCompra,
    int CantidadBoletos,
    decimal PrecioBoleto,
    decimal Importe,
    IReadOnlyList<string> Numeros);

public interface IConfirmedTicketPurchaseRepository
{
    Task<ConfirmedTicketPurchase> GetAsync(
        int sorteoId,
        int userId,
        string folio,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ConfirmedTicketPurchaseSummary>> ListAsync(
        int sorteoId,
        int userId,
        CancellationToken cancellationToken);
}

public sealed class ConfirmedTicketPurchaseRepository : IConfirmedTicketPurchaseRepository
{
    private readonly eLottoContext _context;

    public ConfirmedTicketPurchaseRepository(eLottoContext context) => _context = context;

    public async Task<IReadOnlyList<ConfirmedTicketPurchaseSummary>> ListAsync(
        int sorteoId,
        int userId,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from confirmed in _context.BoletosConfirmados.AsNoTracking()
            join lottery in _context.Sorteos.AsNoTracking()
                on confirmed.SorteosId equals lottery.Id
            where confirmed.SorteosId == sorteoId
                && confirmed.UsuarioId == userId
            select new
            {
                confirmed.FolioCompra,
                confirmed.Numero,
                FechaCompra = confirmed.Fecha,
                lottery.PrecioBoleto,
                lottery.PrecioPorMil,
                lottery.CantidadBoletos
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.FolioCompra, StringComparer.Ordinal)
            .Select(group =>
            {
                var first = group.First();
                var width = first.CantidadBoletos
                    .ToString(CultureInfo.InvariantCulture)
                    .Length;
                var numbers = group
                    .Select(row => row.Numero.PadLeft(width, '0'))
                    .OrderBy(number => number, StringComparer.Ordinal)
                    .ToArray();
                var unitPrice = numbers.Length >= 1000
                    ? first.PrecioPorMil
                    : first.PrecioBoleto;

                return new ConfirmedTicketPurchaseSummary(
                    group.Key,
                    group.Min(row => row.FechaCompra),
                    numbers.Length,
                    unitPrice,
                    unitPrice * numbers.Length,
                    numbers);
            })
            .OrderByDescending(purchase => purchase.FechaCompra)
            .ToArray();
    }

    public async Task<ConfirmedTicketPurchase> GetAsync(
        int sorteoId,
        int userId,
        string folio,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from confirmed in _context.BoletosConfirmados.AsNoTracking()
            join lottery in _context.Sorteos.AsNoTracking()
                on confirmed.SorteosId equals lottery.Id
            join user in _context.Users.AsNoTracking()
                on confirmed.UsuarioId equals user.Id
            where confirmed.SorteosId == sorteoId
                && confirmed.UsuarioId == userId
                && confirmed.FolioCompra == folio
            select new
            {
                confirmed.Numero,
                FechaConfirmacion = confirmed.Fecha,
                confirmed.WhatsAppConfirm,
                lottery.Nombre,
                FechaSorteo = lottery.Fecha,
                lottery.PorcentajeMinimoVenta,
                lottery.CantidadBoletos,
                user.Name,
                user.User,
                user.WhatsApp
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
            return null;

        var first = rows[0];
        var width = first.CantidadBoletos
            .ToString(CultureInfo.InvariantCulture)
            .Length;
        var participant = string.IsNullOrWhiteSpace(first.Name) ? first.User : first.Name;
        var whatsApp = ResolveInternationalWhatsApp(
            first.WhatsAppConfirm,
            first.WhatsApp);
        var numbers = rows
            .Select(row => row.Numero.PadLeft(width, '0'))
            .OrderBy(number => number, StringComparer.Ordinal)
            .ToArray();

        return new(
            sorteoId,
            first.Nombre,
            first.Nombre,
            first.FechaSorteo,
            first.PorcentajeMinimoVenta,
            folio,
            rows.Min(row => row.FechaConfirmacion),
            userId,
            participant,
            whatsApp,
            width,
            numbers);
    }

    private static string ResolveInternationalWhatsApp(
        string confirmedWhatsApp,
        string registeredWhatsApp)
    {
        var confirmedDigits = DigitsOnly(confirmedWhatsApp);
        var registeredDigits = DigitsOnly(registeredWhatsApp);

        if (registeredDigits.Length > 10
            && (confirmedDigits.Length <= 10
                || registeredDigits.EndsWith(confirmedDigits, StringComparison.Ordinal)))
            return $"+{registeredDigits}";

        var digits = confirmedDigits.Length >= 10
            ? confirmedDigits
            : registeredDigits;
        if (digits.Length < 10)
            throw new InvalidOperationException("El usuario no tiene un WhatsApp válido para enviar el comprobante.");

        return $"+{digits}";
    }

    private static string DigitsOnly(string value) =>
        new((value ?? string.Empty).Where(char.IsDigit).ToArray());
}
