using System.Collections.Concurrent;

using eLotto.Options;
using eLotto.Core.Services;
using Microsoft.Extensions.Options;

namespace eLotto.Services;

public interface IWhatsAppResendThrottle
{
    int CooldownSeconds { get; }
    int GetRemainingSeconds(int userId);
    WhatsAppThrottleResult TryAcquire(int userId);
}

public sealed record WhatsAppThrottleResult(bool Acquired, int RemainingSeconds);

public sealed class WhatsAppResendThrottle : IWhatsAppResendThrottle
{
    private readonly int _resendCooldownSeconds;
    private readonly ConcurrentDictionary<int, DateTimeOffset> _nextAllowedByUser = new();

    public WhatsAppResendThrottle(IOptions<WhatsAppOptions> options)
    {
        _resendCooldownSeconds = Math.Max(1, options.Value.ResendCooldownSeconds);
    }

    public int CooldownSeconds => _resendCooldownSeconds;

    public int GetRemainingSeconds(int userId)
    {
        if (!_nextAllowedByUser.TryGetValue(userId, out var nextAllowed))
            return 0;

        return RemainingSeconds(nextAllowed, ApplicationClock.NowOffset);
    }

    public WhatsAppThrottleResult TryAcquire(int userId)
    {
        while (true)
        {
            var now = ApplicationClock.NowOffset;
            if (_nextAllowedByUser.TryGetValue(userId, out var current))
            {
                var remaining = RemainingSeconds(current, now);
                if (remaining > 0)
                    return new(false, remaining);

                var nextAllowed = now.AddSeconds(_resendCooldownSeconds);
                if (_nextAllowedByUser.TryUpdate(userId, nextAllowed, current))
                    return new(true, _resendCooldownSeconds);

                continue;
            }

            if (_nextAllowedByUser.TryAdd(userId, now.AddSeconds(_resendCooldownSeconds)))
                return new(true, _resendCooldownSeconds);
        }
    }

    private static int RemainingSeconds(DateTimeOffset nextAllowed, DateTimeOffset now) =>
        Math.Max(0, (int)Math.Ceiling((nextAllowed - now).TotalSeconds));
}
