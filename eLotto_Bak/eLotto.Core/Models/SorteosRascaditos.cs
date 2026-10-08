using System.Text.Json.Serialization;

namespace eLotto.Core.Models
{
    public class SorteosRascaditos
    {
        public int Id { get; set; }
        public int SorteosId { get; set; }

        [JsonIgnore]
        public int UsuarioId { get; set; }
        public string Folio { get; set; }

        [JsonIgnore]
        public int WalletTransactionOrigenId { get; set; }

        [JsonIgnore]
        public bool EsGanador { get; set; }

        [JsonIgnore]
        public int? SorteosRascaditoPremioId { get; set; }

        [JsonIgnore]
        public decimal? ImportePremio { get; set; }

        [JsonIgnore]
        public int? WalletTransactionPremioId { get; set; }

        [JsonIgnore]
        public string MatrizResultado { get; set; }

        [JsonIgnore]
        public string LineaGanadora { get; set; }
        public bool Revelado { get; set; }
        public DateTime FechaGeneracion { get; set; }
        public DateTime? FechaRevelado { get; set; }

        [JsonIgnore]
        public Sorteos Sorteos { get; set; }

        [JsonIgnore]
        public Users Usuario { get; set; }

        [JsonIgnore]
        public WalletTransaction WalletTransactionOrigen { get; set; }

        [JsonIgnore]
        public SorteosRascaditoPremios SorteosRascaditoPremio { get; set; }

        [JsonIgnore]
        public WalletTransaction WalletTransactionPremio { get; set; }
    }
}
