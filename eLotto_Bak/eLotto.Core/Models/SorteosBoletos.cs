namespace eLotto.Core.Models
{
    public class SorteosBoletos
    {
        public int Id { get; set; }
        public int SorteosId { get; set; }
        public string Numero { get; set; }
        public string FolioCompra { get; set; }
        public int UsuarioId { get; set; }
        public DateTime Fecha { get; set; }
        public bool Pagado { get; set; }
        public bool PreApartado { get; set; }
        public bool Apartado { get; set; }
        public bool Aviso { get; set; }
        public Sorteos Sorteos { get; set; }
    }
}
