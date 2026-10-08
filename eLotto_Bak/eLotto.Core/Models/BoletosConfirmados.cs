namespace eLotto.Core.Models
{
    public class BoletosConfirmados
    {
        public int Id { get; set; }
        public int SorteosId { get; set; }
        public string Numero { get; set; }
        public string FolioCompra { get; set; }
        public int UsuarioId { get; set; }
        public DateTime Fecha { get; set; }
        public int UsuarioIdConfirm { get; set; }
        public string WhatsAppConfirm { get; set; }
        public string CuentaAsignada { get; set; }
    }
}
