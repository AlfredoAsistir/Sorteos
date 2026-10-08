namespace eLotto.Core.Services;

public static class ApplicationClock
{
    public static DateTime Now
    {
        get
        {
            TimeZoneInfo.ClearCachedData();
            return DateTime.Now;
        }
    }

    public static DateTimeOffset NowOffset
    {
        get
        {
            TimeZoneInfo.ClearCachedData();
            return DateTimeOffset.Now;
        }
    }
}
