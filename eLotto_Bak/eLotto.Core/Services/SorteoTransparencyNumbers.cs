namespace eLotto.Core.Services;

public static class SorteoTransparencyNumbers
{
    public static string[] GetUnsold(int total, IReadOnlyCollection<string> sold)
    {
        if (total <= 0)
            throw new InvalidOperationException("El sorteo no tiene una cantidad válida de boletos.");

        var first = total == 60000 ? 1 : 0;
        var width = total.ToString().Length;
        var soldSet = sold.ToHashSet(StringComparer.Ordinal);
        if (sold.Count > total || soldSet.Count != sold.Count ||
            soldSet.Any(number => !int.TryParse(number, out var parsed) ||
                parsed < first || parsed >= first + total ||
                number != parsed.ToString().PadLeft(width, '0')))
            throw new InvalidOperationException("Los números vendidos del sorteo son inconsistentes.");

        var unsold = Enumerable.Range(first, total)
            .Select(number => number.ToString().PadLeft(width, '0'))
            .Where(number => !soldSet.Contains(number))
            .ToArray();
        if (unsold.Length + sold.Count != total)
            throw new InvalidOperationException("La lista de números no vendidos no cuadra con la venta confirmada.");
        return unsold;
    }
}
