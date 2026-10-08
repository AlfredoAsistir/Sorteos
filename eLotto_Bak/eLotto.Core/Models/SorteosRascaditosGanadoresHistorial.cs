namespace eLotto.Core.Models;

public sealed class SorteosRascaditosGanadoresHistorial
{
    public long Id { get; set; }
    public int SorteoId { get; set; }
    public int UsuarioId { get; set; }
    public string Folio { get; set; }
    public decimal ImportePremio { get; set; }
    public string MatrizResultado { get; set; }
    public string LineaGanadora { get; set; }
    public DateTime FechaGeneracion { get; set; }
    public DateTime FechaRevelado { get; set; }
    public int WalletTransactionOrigenId { get; set; }
    public int WalletTransactionPremioId { get; set; }
    public string SorteoNombre { get; set; }
    public string SorteoImagen { get; set; }
    public DateTime FechaArchivado { get; set; }
    public Sorteos Sorteo { get; set; }
    public Users Usuario { get; set; }
    public WalletTransaction WalletTransactionOrigen { get; set; }
    public WalletTransaction WalletTransactionPremio { get; set; }
}
