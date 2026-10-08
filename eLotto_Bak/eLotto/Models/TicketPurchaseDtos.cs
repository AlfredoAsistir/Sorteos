using System.ComponentModel.DataAnnotations;

namespace eLotto.Models;

public sealed class ConfirmTicketPurchaseDto
{
    [Range(1, int.MaxValue)]
    public int SorteoId { get; set; }

    [Required, StringLength(8, MinimumLength = 8)]
    public string FolioCompra { get; set; } = string.Empty;

    [Required, MinLength(1), MaxLength(2000)]
    public List<string> Numeros { get; set; } = [];
}

public sealed record TicketPurchaseDto(
    bool Ok,
    string Codigo,
    string Mensaje,
    decimal Total,
    decimal Saldo,
    IReadOnlyList<string> Numeros,
    IReadOnlyList<string> NoDisponibles);

public sealed record CurrentUserTicketsDto(
    int SorteoId,
    string NombreSorteo,
    DateTime FechaSorteo,
    IReadOnlyList<UserTicketPurchaseDto> Compras,
    int ReenvioDisponibleEnSegundos);

public sealed record UserTicketPurchaseDto(
    string FolioCompra,
    DateTime FechaCompra,
    int CantidadBoletos,
    decimal PrecioBoleto,
    decimal Importe,
    IReadOnlyList<string> Numeros);

public sealed record TicketWhatsAppResendDto(
    bool Ok,
    string Mensaje,
    int ReintentarEnSegundos);
