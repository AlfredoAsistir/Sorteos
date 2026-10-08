using eLotto.Core.Models;

namespace eLotto.Core.Services;

public static class SorteoEstados
{
    public const string Disponible = "disponible";
    public const string ProximoAIniciar = "proximo_a_iniciar";
    public const string EnProceso = "en_proceso";
    public const string Finalizado = "finalizado";
}

public static class SorteoDisponibilidadPolicy
{
    public const int MinutosBloqueoPredeterminados = 90;

    public static DateTimeOffset ObtenerInicioBloqueo(Sorteos sorteo, ISorteoTimeService timeService,
        int minutosBloqueo = MinutosBloqueoPredeterminados) =>
        timeService.ResolveScheduledTime(sorteo).AddMinutes(-minutosBloqueo);

    public static bool VentaDisponible(Sorteos sorteo, DateTimeOffset ahora, ISorteoTimeService timeService,
        int minutosBloqueo = MinutosBloqueoPredeterminados) =>
        string.IsNullOrWhiteSpace(sorteo.NumeroGanador) &&
        ahora < ObtenerInicioBloqueo(sorteo, timeService, minutosBloqueo);

    public static string ObtenerEstado(Sorteos sorteo, DateTimeOffset ahora, ISorteoTimeService timeService,
        int minutosBloqueo = MinutosBloqueoPredeterminados)
    {
        if (!string.IsNullOrWhiteSpace(sorteo.NumeroGanador))
            return SorteoEstados.Finalizado;
        if (ahora < ObtenerInicioBloqueo(sorteo, timeService, minutosBloqueo))
            return SorteoEstados.Disponible;
        return ahora < timeService.ResolveScheduledTime(sorteo)
            ? SorteoEstados.ProximoAIniciar
            : SorteoEstados.EnProceso;
    }
}
