using System.Text.Json.Serialization;

namespace eLotto.Core.Models
{
    public class Sorteos
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Imagen1 { get; set; }
        public string Imagen2 { get; set; }
        public string Imagen3 { get; set; }
        public string Imagen2Tema { get; set; }
        public string Imagen3Tema { get; set; }
        public DateTime Fecha { get; set; }
        public string ZonaHoraria { get; set; } = "CDMX";
        public string UrlTransmisionEnVivo { get; set; }
        public decimal PrecioBoleto { get; set; }
        public decimal PrecioPorMil { get; set; }
        public int CantidadBoletos { get; set; }
        public int PorcentajeMinimoVenta { get; set; }
        public bool RascaditosHabilitados { get; set; }
        public int GanadoresPorGrupo { get; set; }
        public int RascaditosPorGrupo { get; set; }
        public decimal ImporteDepositoStripePorRascadito { get; set; }
        public string NumeroGanador { get; set; }
        public int UsuarioIdGanador { get; set; }
        public string NombreGanador { get; set; }
        public ICollection<SorteosBoletos> SorteosBoletos { get; set; } = new List<SorteosBoletos>();
        public ICollection<SorteosRascaditoPremios> RascaditoPremios { get; set; } = new List<SorteosRascaditoPremios>();

        [JsonIgnore]
        public ICollection<SorteosRascaditos> Rascaditos { get; set; } = new List<SorteosRascaditos>();
    }
}
