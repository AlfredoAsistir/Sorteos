namespace eLotto.Core.Models;

public sealed record ConfirmedTicketPurchase(
    int SorteoId,
    string NombreSorteo,
    string PremioPrincipal,
    DateTime FechaSorteo,
    int PorcentajeMinimoVenta,
    string FolioCompra,
    DateTime FechaConfirmacion,
    int UsuarioId,
    string Participante,
    string WhatsApp,
    int CantidadPosiciones,
    IReadOnlyList<string> Numeros);
