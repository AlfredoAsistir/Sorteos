namespace eLotto.Core.Models
{
    public class GanadoresSorteos
    {
        public int Id { get; set; }
        public int? SorteoId { get; set; }
        public string NumeroGanador { get; set; }
        public string FolioSorteo { get; set; }
        public string Nombre { get; set; }
        public DateTime FechaFin { get; set; }
        public string WhatsAppGanador { get; set; }
        public int UsuarioIdGanador { get; set; }
        public string NombreGanador { get; set; }
        public Sorteos Sorteo { get; set; }
        public ReferralWinnerCashReward ReferralWinnerCashReward { get; set; }
    }
}
