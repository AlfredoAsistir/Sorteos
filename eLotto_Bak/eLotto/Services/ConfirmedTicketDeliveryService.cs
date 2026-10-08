using eLotto.Core.Models;
using eLotto.Core.Repository;
using eLotto.Core.Services;

namespace eLotto.Services;

public interface IConfirmedTicketDeliveryService
{
    Task DeliverAsync(
        int sorteoId,
        int userId,
        string folio,
        CancellationToken cancellationToken);
}

public sealed class ConfirmedTicketDeliveryService : IConfirmedTicketDeliveryService
{
    private readonly IConfirmedTicketPurchaseRepository _repository;
    private readonly IConfirmedTicketsPdfService _pdfService;
    private readonly IWhatsAppService _whatsAppService;
    private readonly ILogger<ConfirmedTicketDeliveryService> _logger;

    public ConfirmedTicketDeliveryService(
        IConfirmedTicketPurchaseRepository repository,
        IConfirmedTicketsPdfService pdfService,
        IWhatsAppService whatsAppService,
        ILogger<ConfirmedTicketDeliveryService> logger)
    {
        _repository = repository;
        _pdfService = pdfService;
        _whatsAppService = whatsAppService;
        _logger = logger;
    }

    public async Task DeliverAsync(
        int sorteoId,
        int userId,
        string folio,
        CancellationToken cancellationToken)
    {
        ConfirmedTicketPurchase purchase;
        try
        {
            purchase = await _repository.GetAsync(sorteoId, userId, folio, cancellationToken)
                ?? throw new InvalidOperationException("No se encontró la compra confirmada para generar el comprobante.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falló la etapa {Stage} para SorteoId {SorteoId}, FolioCompra {FolioCompra}, UsuarioId {UsuarioId}.",
                "official_purchase_query",
                sorteoId,
                folio,
                userId);
            throw;
        }

        ConfirmedTicketsPdfFile pdf;
        try
        {
            pdf = await _pdfService.CreateOrGetAsync(purchase, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falló la etapa {Stage} para SorteoId {SorteoId}, FolioCompra {FolioCompra}, UsuarioId {UsuarioId}.",
                "pdf_generation_storage",
                sorteoId,
                folio,
                userId);
            throw;
        }

        try
        {
            var sent = await _whatsAppService.SendDocumentAsync(
                purchase.WhatsApp,
                pdf.FileName,
                pdf.Content,
                BuildWhatsAppMessage(purchase.NombreSorteo),
                cancellationToken);
            if (!sent)
                throw new InvalidOperationException("El proveedor de WhatsApp no confirmó el envío del documento.");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falló la etapa {Stage} para SorteoId {SorteoId}, FolioCompra {FolioCompra}, UsuarioId {UsuarioId}.",
                "whatsapp_document_send",
                sorteoId,
                folio,
                userId);
            throw;
        }

        _logger.LogInformation(
            "Comprobante de boletos enviado para SorteoId {SorteoId}, FolioCompra {FolioCompra}, UsuarioId {UsuarioId}.",
            sorteoId,
            folio,
            userId);
    }

    private static string BuildWhatsAppMessage(string lotteryName)
    {
        var safeName = string.IsNullOrWhiteSpace(lotteryName)
            ? "el sorteo"
            : lotteryName.Trim();
        if (safeName.Length > 120)
            safeName = safeName[..120];

        return $"🍀 ¡Gracias por tu compra! Tus boletos para {safeName} ya están confirmados y participan oficialmente. Adjuntamos tu comprobante con todos tus números. ¡Mucha suerte! 🤞🎟️";
    }
}
