using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace eLotto.Services;

internal static class SorteoTransparencyPdf
{
    private static readonly string[] Fonts =
    [
        "eLotto Ticket Oxanium",
        "eLotto Ticket Play",
        "eLotto Ticket ShareTech"
    ];

    public static byte[] Generate(int sorteoId, string nombre, string applicationName, DateTime fechaSorteo,
        string zonaHoraria,
        DateTime fechaCierre, DateTime fechaEmision, int total, int vendidos,
        IReadOnlyList<string> numeros)
    {
        ConfirmedTicketsPdfService.EnsureTicketFontsRegistered();
        if (numeros.Count + vendidos != total)
            throw new InvalidOperationException("La cantidad de números no vendidos es inconsistente.");

        var document = Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginHorizontal(24);
            page.MarginVertical(24);
            page.DefaultTextStyle(style => style.FontFamily("Lato").FontSize(8).FontColor("#111827"));
            page.Header().Column(column =>
            {
                column.Item().Text("NÚMEROS NO VENDIDOS · DOCUMENTO DE TRANSPARENCIA")
                    .Bold().FontSize(12).FontColor("#0F766E");
                column.Item().Text($"Sorteo: {nombre} · ID {sorteoId} · Programado: {fechaSorteo:dd/MM/yyyy HH:mm} ({zonaHoraria})");
                column.Item().Text($"Ventas cerradas: {Format(fechaCierre)} · Documento emitido: {Format(fechaEmision)}");
                column.Item().Text($"Cantidad de Boletos No vendidos: {numeros.Count:N0}");
                column.Item().PaddingVertical(8).LineHorizontal(1).LineColor("#CBD5E1");
            });
            page.Content().PaddingVertical(4).Column(column =>
            {
                column.Spacing(1);
                if (numeros.Count == 0)
                    column.Item().Text("Todos los boletos fueron vendidos.").Bold();
                for (var start = 0; start < numeros.Count; start += 14)
                {
                    var first = start;
                    column.Item().Row(row =>
                    {
                        for (var columnIndex = 0; columnIndex < 14; columnIndex++)
                        {
                            var index = first + columnIndex;
                            var cell = row.RelativeItem().AlignCenter();
                            if (index < numeros.Count)
                            {
                                var number = numeros[index];
                                cell.Text(number).FontFamily(FontFor(sorteoId, number)).FontSize(10);
                            }
                            else cell.Text(" ");
                        }
                    });
                }
            });
            page.Footer().Row(row =>
            {
                row.RelativeItem().Text(FooterText(applicationName)).FontSize(7);
                row.AutoItem().DefaultTextStyle(style => style.FontSize(7)).Text(text =>
                {
                    text.Span("Página ");
                    text.CurrentPageNumber();
                    text.Span(" de ");
                    text.TotalPages();
                });
            });
            page.Foreground().Element(container => ComposeWatermark(container, applicationName, sorteoId));
        }));
        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return stream.ToArray();
    }

    internal static string Format(DateTime value) =>
        value.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);

    internal static string FooterText(string applicationName) =>
        $"Relación de boletos no vendidos {applicationName}";

    private static void ComposeWatermark(IContainer container, string applicationName, int sorteoId)
    {
        container.Padding(10).Column(column =>
        {
            column.Spacing(15);
            for (var rowIndex = 0; rowIndex < 12; rowIndex++)
            {
                var currentRow = rowIndex;
                column.Item().Height(48).PaddingLeft(currentRow % 2 == 0 ? 0 : 45)
                    .Row(row =>
                    {
                        for (var columnIndex = 0; columnIndex < 2; columnIndex++)
                        {
                            row.RelativeItem().AlignCenter().AlignMiddle().Rotate(-28)
                                .Text(applicationName)
                                .FontFamily(FontFor(sorteoId, $"WATERMARK|{currentRow}|{columnIndex}"))
                                .FontSize(13).FontColor("#300F766E");
                        }
                    });
            }
        });
    }

    private static string FontFor(int sorteoId, string number)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{sorteoId}:{number}"));
        return Fonts[bytes[0] % Fonts.Length];
    }
}
