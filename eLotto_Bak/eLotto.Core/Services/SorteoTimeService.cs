using eLotto.Core.Models;

namespace eLotto.Core.Services;

public sealed class SorteoTimeService : ISorteoTimeService
{
    public SorteoTimeSnapshot GetCurrent(Sorteos sorteo)
    {
        var serverNow = ApplicationClock.NowOffset;
        return new SorteoTimeSnapshot(serverNow, ConvertToSorteoTime(serverNow, sorteo));
    }

    public DateTimeOffset ConvertToSorteoTime(DateTimeOffset serverMoment, Sorteos sorteo)
    {
        return TimeZoneInfo.ConvertTime(serverMoment, GetTimeZone(sorteo));
    }

    public TimeSpan GetOffsetDifference(DateTimeOffset serverMoment, Sorteos sorteo)
    {
        return ConvertToSorteoTime(serverMoment, sorteo).Offset - serverMoment.Offset;
    }

    public DateTimeOffset ResolveScheduledTime(Sorteos sorteo)
    {
        var timeZone = GetTimeZone(sorteo);
        // Fecha es la hora de pared que el administrador capturó para este sorteo.
        var scheduled = DateTime.SpecifyKind(sorteo.Fecha, DateTimeKind.Unspecified);

        if (timeZone.IsInvalidTime(scheduled))
            throw new ArgumentException(
                $"La fecha programada {scheduled:yyyy-MM-dd HH:mm} no existe en la zona horaria {sorteo.ZonaHoraria}.",
                nameof(sorteo));

        if (timeZone.IsAmbiguousTime(scheduled))
            throw new ArgumentException(
                $"La fecha programada {scheduled:yyyy-MM-dd HH:mm} ocurre dos veces en la zona horaria {sorteo.ZonaHoraria}.",
                nameof(sorteo));

        return new DateTimeOffset(scheduled, timeZone.GetUtcOffset(scheduled));
    }

    public DateTimeOffset ResolveRecordedTime(DateTime recordedLocalTime, Sorteos sorteo)
    {
        var timeZone = GetTimeZone(sorteo);
        var recorded = DateTime.SpecifyKind(recordedLocalTime, DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(recorded))
            throw new ArgumentException("La fecha registrada no existe en la zona horaria del sorteo.",
                nameof(recordedLocalTime));

        // Una hora repetida se interpreta conservadoramente como la segunda ocurrencia:
        // así un depósito pendiente nunca se cancela antes de cumplir su plazo.
        var offset = timeZone.IsAmbiguousTime(recorded)
            ? timeZone.GetAmbiguousTimeOffsets(recorded).Min()
            : timeZone.GetUtcOffset(recorded);
        return new DateTimeOffset(recorded, offset);
    }

    private static TimeZoneInfo GetTimeZone(Sorteos sorteo)
    {
        ArgumentNullException.ThrowIfNull(sorteo);

        if (string.IsNullOrWhiteSpace(sorteo.ZonaHoraria))
            throw new ArgumentException("El sorteo no tiene una zona horaria válida.", nameof(sorteo));

        // El catálogo guarda nombres cortos; TimeZoneInfo necesita el identificador IANA.
        // Los identificadores completos anteriores siguen siendo legibles para sorteos existentes.
        var zoneId = sorteo.ZonaHoraria == "CDMX"
            ? "America/Mexico_City"
            : sorteo.ZonaHoraria.StartsWith("America/", StringComparison.Ordinal)
                ? sorteo.ZonaHoraria
                : $"America/{sorteo.ZonaHoraria}";
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        }
        catch (TimeZoneNotFoundException) when (OperatingSystem.IsWindows() && zoneId == "America/Ciudad_Juarez")
        {
            // Algunas versiones de Windows no incluyen este ID IANA reciente.
            return TimeZoneInfo.FindSystemTimeZoneById("Mountain Standard Time");
        }
    }
}
