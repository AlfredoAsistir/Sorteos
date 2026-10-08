using eLotto.Core.Models;

namespace eLotto.Core.Services;

public interface ISorteoTimeService
{
    SorteoTimeSnapshot GetCurrent(Sorteos sorteo);
    DateTimeOffset ConvertToSorteoTime(DateTimeOffset serverMoment, Sorteos sorteo);
    TimeSpan GetOffsetDifference(DateTimeOffset serverMoment, Sorteos sorteo);
    DateTimeOffset ResolveScheduledTime(Sorteos sorteo);
    DateTimeOffset ResolveRecordedTime(DateTime recordedLocalTime, Sorteos sorteo);
}

public sealed record SorteoTimeSnapshot(DateTimeOffset ServerNow, DateTimeOffset SorteoNow)
{
    public TimeSpan OffsetDifference => SorteoNow.Offset - ServerNow.Offset;
}
