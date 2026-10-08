using System.Globalization;
using System.Text;
using eLotto.Core.Models;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace eLotto.Services;

public sealed record ConfirmedTicketsPdfFile(string FullPath, string FileName, byte[] Content);

public interface IConfirmedTicketsPdfService
{
    Task<ConfirmedTicketsPdfFile> CreateOrGetAsync(ConfirmedTicketPurchase purchase, CancellationToken cancellationToken);
}

public sealed class ConfirmedTicketsPdfService : IConfirmedTicketsPdfService
{
    private const string RootFolderName = "BoletosConfirmados";

    private static readonly string[] TicketFontNames =
    [
        "eLotto Ticket Oxanium",
        "eLotto Ticket Play",
        "eLotto Ticket ShareTech"
    ];

    private static readonly (string ResourceName, string FontName)[] TicketFontResources =
    [
        ("eLotto.Resources.Fonts.Oxanium-Regular.ttf", TicketFontNames[0]),
        ("eLotto.Resources.Fonts.Play-Regular.ttf", TicketFontNames[1]),
        ("eLotto.Resources.Fonts.ShareTech-Regular.ttf", TicketFontNames[2])
    ];

    private static readonly object FontRegistrationLock = new();
    private static bool _fontsRegistered;

    private readonly IWebHostEnvironment _environment;

    public ConfirmedTicketsPdfService(IWebHostEnvironment environment)
    {
        _environment = environment;
        EnsureTicketFontsRegistered();
    }

    public async Task<ConfirmedTicketsPdfFile> CreateOrGetAsync(ConfirmedTicketPurchase purchase, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(purchase);
        cancellationToken.ThrowIfCancellationRequested();

        var root = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, RootFolderName));
        var safeSorteo = SanitizeSegment(purchase.SorteoId.ToString(CultureInfo.InvariantCulture));
        var safeUser = SanitizeSegment(GetLocalWhatsApp(purchase.WhatsApp));
        var safeFolio = SanitizeSegment(purchase.FolioCompra);

        var directory = Path.GetFullPath(Path.Combine(root, safeSorteo, safeUser));
        var fileName = $"{safeFolio}.pdf";
        var fullPath = Path.GetFullPath(Path.Combine(directory, fileName));

        EnsureContained(root, fullPath);

        Directory.CreateDirectory(directory);

        if (File.Exists(fullPath))
            return new(fullPath, fileName, await File.ReadAllBytesAsync(fullPath, cancellationToken));

        var pdfBytes = GeneratePdf(purchase);
        cancellationToken.ThrowIfCancellationRequested();

        var temporaryPath = Path.Combine(directory, $".{safeFolio}.{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllBytesAsync(temporaryPath, pdfBytes, cancellationToken);

            try
            {
                File.Move(temporaryPath, fullPath);
                return new(fullPath, fileName, pdfBytes);
            }
            catch (IOException) when (File.Exists(fullPath))
            {
                return new(fullPath, fileName, await File.ReadAllBytesAsync(fullPath, cancellationToken));
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static byte[] GeneratePdf(ConfirmedTicketPurchase purchase)
    {
        using var stream = new MemoryStream();

        var document = ConfirmedTicketsDocument.Create(purchase);
        document.GeneratePdf(stream);

        return stream.ToArray();
    }

    internal static void EnsureTicketFontsRegistered()
    {
        if (_fontsRegistered)
            return;

        lock (FontRegistrationLock)
        {
            if (_fontsRegistered)
                return;

            var assembly = typeof(ConfirmedTicketsPdfService).Assembly;

            foreach (var font in TicketFontResources)
            {
                using var stream = assembly.GetManifestResourceStream(font.ResourceName)
                    ?? throw new InvalidOperationException($"No se encontró el recurso de fuente embebida {font.ResourceName}.");

                FontManager.RegisterFontWithCustomName(font.FontName, stream);
            }

            _fontsRegistered = true;
        }
    }

    private static string SanitizeSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("No se puede construir la ruta del PDF con un segmento vacío.");

        var sanitized = new StringBuilder(value.Length);

        foreach (var character in value.Trim())
            sanitized.Append(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' ? character : '_');

        var result = sanitized.ToString().Trim('_');

        if (string.IsNullOrWhiteSpace(result) || result is "." or "..")
            throw new InvalidOperationException("No se puede construir la ruta del PDF con un segmento inválido.");

        return result;
    }

    private static string GetLocalWhatsApp(string whatsApp)
    {
        var digits = new string((whatsApp ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length > 10 ? digits[^10..] : digits;
    }

    private static void EnsureContained(string root, string fullPath)
    {
        var rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        if (!fullPath.StartsWith(rootWithSeparator, comparison))
            throw new InvalidOperationException("La ruta del PDF sale de la carpeta permitida.");
    }

    private sealed class ConfirmedTicketsDocument : IDocument
    {
        private readonly ConfirmedTicketPurchase _purchase;

        private ConfirmedTicketsDocument(ConfirmedTicketPurchase purchase)
        {
            _purchase = purchase;
        }

        public static IDocument Create(ConfirmedTicketPurchase purchase)
        {
            return new ConfirmedTicketsDocument(purchase);
        }

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(36);
                page.MarginVertical(30);

                page.DefaultTextStyle(style => style
                    .FontFamily("Lato")
                    .FontSize(10)
                    .FontColor("#1F2937"));

                page.Header().Element(ComposeHeader);
                page.Content().PaddingVertical(18).Element(ComposeContent);
                page.Footer().Element(ComposeFooter);

                page.Foreground().Element(ComposeWatermark);
            });
        }

        private void ComposeHeader(IContainer container)
        {
            container
                .PaddingBottom(10)
                .BorderBottom(1)
                .BorderColor("#CBD5E1")
                .Row(row =>
                {
                    row.RelativeItem().Column(column =>
                    {
                        column.Item()
                            .Text("BOLETOS CONFIRMADOS")
                            .FontSize(17)
                            .SemiBold()
                            .FontColor("#0F766E");

                        column.Item()
                            .PaddingTop(2)
                            .Text(_purchase.NombreSorteo)
                            .FontSize(11)
                            .SemiBold();

                        column.Item()
                            .PaddingTop(4)
                            .Text($"Este sorteo está programado para realizarse con la Lotería Nacional el {_purchase.FechaSorteo.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}, siempre que se venda al menos el {_purchase.PorcentajeMinimoVenta} % de los boletos. Si no se alcanza ese mínimo, se reprogramará para una nueva fecha.")
                            .FontSize(9);

                        column.Item()
                            .PaddingTop(2)
                            .Text("El número ganador será el del primer lugar del sorteo de la Lotería Nacional en la fecha en que finalmente se realice.")
                            .FontSize(9);
                    });

                    row.ConstantItem(150).AlignRight().Column(column =>
                    {
                        column.Item()
                            .AlignRight()
                            .Text($"Folio: {_purchase.FolioCompra}")
                            .SemiBold();

                        column.Item()
                            .PaddingTop(3)
                            .AlignRight()
                            .Text("PAGO CONFIRMADO")
                            .FontSize(9)
                            .SemiBold()
                            .FontColor("#15803D");
                    });
                });
        }

        private void ComposeContent(IContainer container)
        {
            var columnCount = GetTicketColumnCount(_purchase.Numeros.Count);

            container.Column(column =>
            {
                column.Spacing(12);

                column.Item()
                    .Border(1)
                    .BorderColor("#99F6E4")
                    .Background("#F0FDFA")
                    .Padding(12)
                    .Text(text =>
                    {
                        text.Span("Compra confirmada. ").SemiBold().FontColor("#0F766E");
                        text.Span("Los números de este documento son los boletos oficiales registrados para tu compra.");
                    });

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    table.Cell().ColumnSpan(2)
                        .ShowEntire()
                        .BorderBottom(1)
                        .BorderColor("#E2E8F0")
                        .PaddingVertical(7)
                        .PaddingHorizontal(8)
                        .Text(text =>
                        {
                            text.Span("Sorteo\n").FontSize(8).FontColor("#64748B");
                            text.Span(_purchase.NombreSorteo ?? string.Empty).SemiBold().FontColor("#0F172A");
                        });

                    AddSummaryCell(table, "Fecha del sorteo", _purchase.FechaSorteo.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
                    AddSummaryCell(table, "Fecha de confirmación", _purchase.FechaConfirmacion.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture));
                    AddSummaryCell(table, "Participante", _purchase.Participante);
                    AddSummaryCell(table, "Cantidad total", $"{_purchase.Numeros.Count} boletos");
                });

                column.Item()
                    .PaddingTop(4)
                    .Text("Números oficiales")
                    .FontSize(14)
                    .SemiBold()
                    .FontColor("#0F172A");

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        for (var index = 0; index < columnCount; index++)
                            columns.RelativeColumn();
                    });

                    foreach (var number in _purchase.Numeros)
                    {
                        table.Cell()
                            .ShowEntire()
                            .Height(27)
                            .Padding(3)
                            .Border(0.7f)
                            .BorderColor("#CBD5E1")
                            .Background("#FFFFFF")
                            .AlignCenter()
                            .AlignMiddle()
                            .Text(number)
                            .FontFamily(SelectTicketFont(_purchase.FolioCompra, number))
                            .FontSize(GetTicketFontSize(_purchase.CantidadPosiciones))
                            .FontColor("#111827");
                    }
                });
            });
        }

        private static void AddSummaryCell(TableDescriptor table, string label, string value)
        {
            table.Cell()
                .ShowEntire()
                .BorderBottom(1)
                .BorderColor("#E2E8F0")
                .PaddingVertical(7)
                .PaddingHorizontal(8)
                .Text(text =>
                {
                    text.Span($"{label}\n").FontSize(8).FontColor("#64748B");
                    text.Span(value ?? string.Empty).SemiBold().FontColor("#0F172A");
                });
        }

        private void ComposeWatermark(IContainer container)
        {
            container.Padding(10).Column(column =>
            {
                column.Spacing(15);

                for (var rowIndex = 0; rowIndex < 12; rowIndex++)
                {
                    var currentRow = rowIndex;

                    column.Item()
                        .Height(52)
                        .PaddingLeft(currentRow % 2 == 0 ? 0 : 45)
                        .Row(row =>
                        {
                            for (var columnIndex = 0; columnIndex < 2; columnIndex++)
                            {
                                var currentColumn = columnIndex;

                                row.RelativeItem()
                                    .AlignCenter()
                                    .AlignMiddle()
                                    .Rotate(-28)
                                    .Text($"Folio: {_purchase.FolioCompra} | PAGO CONFIRMADO")
                                    .FontFamily(SelectWatermarkFont(currentRow, currentColumn))
                                    .FontSize(13)
                                    .FontColor("#300F766E");
                            }
                        });
                }
            });
        }

        private void ComposeFooter(IContainer container)
        {
            container
                .PaddingTop(8)
                .BorderTop(1)
                .BorderColor("#CBD5E1")
                .Row(row =>
                {
                    row.RelativeItem()
                        .Text($"Folio {_purchase.FolioCompra}")
                        .FontSize(8)
                        .FontColor("#64748B");

                    row.AutoItem().Text(text =>
                    {
                        text.DefaultTextStyle(style => style.FontSize(8).FontColor("#64748B"));
                        text.Span("Página ");
                        text.CurrentPageNumber();
                        text.Span(" de ");
                        text.TotalPages();
                    });
                });
        }

        private static int GetTicketColumnCount(int ticketCount) => ticketCount switch
        {
            <= 12 => 3,
            <= 80 => 4,
            _ => 6
        };

        private static float GetTicketFontSize(int width) => width switch
        {
            <= 5 => 11,
            <= 7 => 10,
            _ => 9
        };

        private string SelectWatermarkFont(int rowIndex, int columnIndex)
        {
            return SelectTicketFont(_purchase.FolioCompra, $"WATERMARK|{rowIndex}|{columnIndex}");
        }

        private static string SelectTicketFont(string folio, string number)
        {
            const uint offsetBasis = 2166136261;
            const uint prime = 16777619;

            var hash = offsetBasis;

            foreach (var value in Encoding.UTF8.GetBytes($"{folio}|{number}"))
            {
                hash ^= value;
                hash *= prime;
            }

            return TicketFontNames[hash % TicketFontNames.Length];
        }
    }
}
