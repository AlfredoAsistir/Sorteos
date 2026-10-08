using System.ComponentModel.DataAnnotations;
using eLotto.Core.Models;

namespace eLotto.Models;

public sealed class VerificarNumeroGanadorRequest
{
    [Required]
    [RegularExpression("^[0-9]+$", ErrorMessage = "El número ganador solo puede contener dígitos.")]
    [MaxLength(32)]
    public string NumeroGanador { get; set; } = string.Empty;
}

public sealed class ReprogramarSorteoRequest
{
    [RegularExpression("^[0-9]+$", ErrorMessage = "El número ganador solo puede contener dígitos.")]
    [MaxLength(32)]
    public string NumeroGanador { get; set; }
}

public sealed record VerificarNumeroGanadorResponse(
    int SorteoId,
    string NumeroGanador,
    bool HayGanador,
    int? UsuarioIdGanador,
    string UsuarioGanador,
    string WhatsAppGanador,
    string FolioCompra,
    ReferenciaGanadorResponse Referencia);

public sealed record ReferenciaGanadorResponse(
    string ReferidorNombre,
    bool Finalizado,
    bool? PremioGenerado,
    int? BoletosRequeridos,
    int? BoletosConfirmados,
    decimal? ImportePremio,
    ReferralWinnerCashRewardStatus? Estado,
    DateTime? FechaPago);

public sealed record GanadorFinalizadoResponse(
    int SorteoId,
    string SorteoNombre,
    string NumeroGanador,
    string UsuarioGanador,
    string FolioCompra,
    DateTime FechaFinalizacion,
    ReferenciaGanadorResponse Referencia);

public sealed record ReprogramarSorteoResponse(
    int SorteoId,
    string NumeroGanador,
    DateTime NuevaFecha,
    int SorteosReprogramados);

public sealed record FinalizarSorteoResponse(
    int SorteoId,
    string NumeroGanador,
    int ComprasArchivadas,
    int RascaditosGanadoresArchivados,
    int RascaditosCaducados,
    GanadorFinalizadoResponse Ganador);

