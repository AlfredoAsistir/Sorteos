using System.ComponentModel.DataAnnotations;

namespace eLotto.Models
{
    public class SorteoFormDto : IValidatableObject
    {
        public int Id { get; set; }
        [Required, StringLength(150)] public string Nombre { get; set; }
        [Required] public DateTime Fecha { get; set; }
        [Required, StringLength(64)] public string ZonaHoraria { get; set; } = "CDMX";
        [Url, StringLength(2048)] public string UrlTransmisionEnVivo { get; set; }
        [Range(0, double.MaxValue)] public decimal PrecioBoleto { get; set; }
        [Range(0, double.MaxValue)] public decimal PrecioPorMil { get; set; }
        [Range(1, int.MaxValue)] public int CantidadBoletos { get; set; }
        [Range(1, 99)] public int PorcentajeMinimoVenta { get; set; }
        public bool RascaditosHabilitados { get; set; }
        public int GanadoresPorGrupo { get; set; }
        public int RascaditosPorGrupo { get; set; }
        public decimal ImporteDepositoStripePorRascadito { get; set; }
        public List<SorteoRascaditoPremioFormDto> RascaditoPremios { get; set; } = new();
        public IFormFile Imagen1 { get; set; }
        public IFormFile Imagen2 { get; set; }
        public IFormFile Imagen3 { get; set; }
        [StringLength(16)] public string Imagen2Tema { get; set; }
        [StringLength(16)] public string Imagen3Tema { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (!ZonasHorariasMexico.Contains(ZonaHoraria))
                yield return new ValidationResult(
                    "Selecciona una zona horaria de México válida.",
                    new[] { nameof(ZonaHoraria) });

            if (!string.IsNullOrWhiteSpace(UrlTransmisionEnVivo) &&
                (!Uri.TryCreate(UrlTransmisionEnVivo, UriKind.Absolute, out var liveUri) ||
                 liveUri.Scheme != Uri.UriSchemeHttps))
            {
                yield return new ValidationResult(
                    "La URL de transmisión debe ser una dirección HTTPS válida.",
                    new[] { nameof(UrlTransmisionEnVivo) });
            }

            if (!RascaditosHabilitados) yield break;
            var premios = RascaditoPremios ?? new List<SorteoRascaditoPremioFormDto>();

            if (GanadoresPorGrupo <= 0)
                yield return new ValidationResult(
                    "GanadoresPorGrupo debe ser mayor que cero.",
                    new[] { nameof(GanadoresPorGrupo) });

            if (RascaditosPorGrupo <= 0)
                yield return new ValidationResult(
                    "RascaditosPorGrupo debe ser mayor que cero.",
                    new[] { nameof(RascaditosPorGrupo) });

            if (GanadoresPorGrupo > RascaditosPorGrupo)
                yield return new ValidationResult(
                    "GanadoresPorGrupo no puede ser mayor que RascaditosPorGrupo.",
                    new[] { nameof(GanadoresPorGrupo), nameof(RascaditosPorGrupo) });

            if (ImporteDepositoStripePorRascadito <= 0)
                yield return new ValidationResult(
                    "ImporteDepositoStripePorRascadito debe ser mayor que cero.",
                    new[] { nameof(ImporteDepositoStripePorRascadito) });

            if (premios.Count == 0)
                yield return new ValidationResult(
                    "Agrega al menos un premio para habilitar los rascaditos.",
                    new[] { nameof(RascaditoPremios) });

            for (var index = 0; index < premios.Count; index++)
            {
                var premio = premios[index];
                var prefix = $"{nameof(RascaditoPremios)}[{index}]";

                if (premio.Premio <= 0)
                    yield return new ValidationResult(
                        "El importe del premio debe ser mayor que cero.",
                        new[] { $"{prefix}.{nameof(premio.Premio)}" });

                if (premio.Cantidad <= 0)
                    yield return new ValidationResult(
                        "La cantidad debe ser mayor que cero.",
                        new[] { $"{prefix}.{nameof(premio.Cantidad)}" });

                if (premio.Entregados < 0 || premio.Entregados > premio.Cantidad)
                    yield return new ValidationResult(
                        "Entregados debe estar entre cero y la cantidad configurada.",
                        new[] { $"{prefix}.{nameof(premio.Entregados)}" });
            }
        }

        private static readonly HashSet<string> ZonasHorariasMexico = new(StringComparer.Ordinal)
        {
            "CDMX", "Cancun", "Merida", "Monterrey",
            "Matamoros", "Chihuahua", "Ciudad_Juarez", "Ojinaga",
            "Mazatlan", "Bahia_Banderas", "Hermosillo", "Tijuana"
        };
    }

    public class SorteoRascaditoPremioFormDto
    {
        public int Id { get; set; }
        public decimal Premio { get; set; }
        public int Cantidad { get; set; }
        public int Entregados { get; set; }
    }
}
