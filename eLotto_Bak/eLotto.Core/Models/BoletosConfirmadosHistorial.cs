namespace eLotto.Core.Models;

public sealed class BoletosConfirmadosHistorial
{
    public long Id { get; set; }
    public int SorteoId { get; set; }
    public int UsuarioId { get; set; }
    public string FolioCompra { get; set; }
    public string NumerosJson { get; set; }
    public int CantidadBoletos { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal ImporteTotal { get; set; }
    public DateTime FechaCompra { get; set; }
    public string WhatsAppConfirm { get; set; }
    public string CuentaAsignada { get; set; }
    public string SorteoNombre { get; set; }
    public DateTime FechaSorteo { get; set; }
    public DateTime FechaArchivado { get; set; }
    public Sorteos Sorteo { get; set; }
    public Users Usuario { get; set; }
}
