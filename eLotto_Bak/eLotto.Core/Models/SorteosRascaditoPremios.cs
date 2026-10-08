using System.Text.Json.Serialization;

namespace eLotto.Core.Models
{
    public class SorteosRascaditoPremios
    {
        public int Id { get; set; }
        public int SorteosId { get; set; }
        public decimal Premio { get; set; }
        public int Cantidad { get; set; }
        public int Entregados { get; set; }

        [JsonIgnore]
        public Sorteos Sorteos { get; set; }
    }
}
