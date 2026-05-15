using System.Globalization;
using System.Security.Cryptography;
using PlayMe.Application.Abstractions;
using PlayMe.Infrastructure.Redis;
using StackExchange.Redis;

namespace PlayMe.Infrastructure.RateLimiting;

/// <summary>
/// Redis-backed sliding-window rate limiter (docs/security.md §5,
/// state.md §1 key schema). Each (policy, subject) pair owns one sorted
/// set whose members are recent acquisition timestamps in milliseconds.
/// The window-trim + count + add is atomic via a server-side Lua script
/// so two near-simultaneous calls can't both squeeze under the limit.
/// </summary>
public sealed class RedisRateLimiter : IRateLimiter
{
    private const string AcquireLua = """
        local now = tonumber(ARGV[1])
        local window = tonumber(ARGV[2])
        local limit = tonumber(ARGV[3])
        redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', now - window)
        local count = redis.call('ZCARD', KEYS[1])
        if count >= limit then
          return 0
        end
        redis.call('ZADD', KEYS[1], now, ARGV[4])
        redis.call('PEXPIRE', KEYS[1], window)
        return 1
        """;

    private readonly IConnectionMultiplexer _redis;
    private readonly IClock _clock;

    public RedisRateLimiter(IConnectionMultiplexer redis, IClock clock)
    {
        _redis = redis;
        _clock = clock;
    }

    public async Task<bool> TryAcquireAsync(
        RateLimitPolicy policy,
        string subjectKey,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(subjectKey);
        ct.ThrowIfCancellationRequested();

        var key = RedisKeys.Rate(policy.Name, subjectKey);
        var nowMs = _clock.UtcNow.ToUnixTimeMilliseconds();
        var windowMs = (long)policy.Window.TotalMilliseconds;

        // Each acquisition needs a unique member, otherwise a same-ms
        // burst would collapse to one ZADD via score-update semantics.
        var member = $"{nowMs.ToString(CultureInfo.InvariantCulture)}:{NewNonce()}";

        var db = _redis.GetDatabase();
        var result = await db.ScriptEvaluateAsync(
            AcquireLua,
            keys: new RedisKey[] { key },
            values: new RedisValue[] { nowMs, windowMs, policy.Limit, member });
        return (long)result == 1L;
    }

    private static string NewNonce()
    {
        Span<byte> buffer = stackalloc byte[6];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToHexString(buffer);
    }
}
