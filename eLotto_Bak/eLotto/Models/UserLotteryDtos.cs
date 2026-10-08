using System.ComponentModel.DataAnnotations;

namespace eLotto.Models;

public sealed record CurrentLotteryDto(
    int Id,
    string Nombre,
    DateTime Fecha,
    string ZonaHoraria,
    DateTime FechaInicioBloqueo,
    string Estado,
    bool VentaDisponible,
    int SegundosParaInicio,
    int SegundosParaCierreVentas,
    string UrlTransmisionEnVivo,
    decimal PrecioBoleto,
    decimal PrecioPorMil,
    int CantidadBoletos,
    int BoletosVendidos,
    decimal PorcentajeVenta,
    int PorcentajeMinimoVenta,
    bool VentaMinimaAlcanzada,
    bool RascaditosHabilitados,
    decimal BolsaRascaditosDisponible,
    string Imagen1,
    string Imagen2,
    string Imagen3,
    string Imagen2Tema,
    string Imagen3Tema);

public sealed record TicketAvailabilityDto(string Numero, bool Disponible);
public sealed record ManualTicketQueryResponseDto(string FolioCompra, IReadOnlyList<TicketAvailabilityDto> Boletos);

public sealed class ManualTicketQueryDto
{
    [Required, MaxLength(20000)] public string Numeros { get; set; } = string.Empty;
}

public sealed class ManualPreReserveDto
{
    [Required, StringLength(8, MinimumLength = 8)] public string FolioCompra { get; set; } = string.Empty;
    [Required, MinLength(1), MaxLength(2000)] public List<string> Numeros { get; set; } = [];
}

public sealed class RandomPreReserveDto
{
    [Range(1, 2000)] public int Cantidad { get; set; }
    [Required] public string Tipo { get; set; } = "azar";
    [MaxLength(10)] public string Valor { get; set; }
}

public sealed record PreReserveDto(
    bool Ok,
    string FolioCompra,
    DateTimeOffset? Expira,
    IReadOnlyList<string> Numeros,
    IReadOnlyList<string> NoDisponibles,
    string Mensaje,
    int SegundosParaExpirar);
