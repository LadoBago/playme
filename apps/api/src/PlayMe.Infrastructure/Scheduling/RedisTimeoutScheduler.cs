using PlayMe.Application.Abstractions;
using PlayMe.Domain.Platform;
using StackExchange.Redis;

namespace PlayMe.Infrastructure.Scheduling;

/// <summary>
/// Backs <see cref="ITimeoutScheduler"/> by the <c>playme:timeouts</c>
/// sorted set (see state.md §2.2 and RedisKeys.Timeouts). One entry per
/// room — <c>ZADD</c> overwrites any existing score so re-scheduling on
/// every accepted move is a single round-trip.
/// </summary>
public sealed class RedisTimeoutScheduler : ITimeoutScheduler
{
    private readonly IConnectionMultiplexer _redis;

    public RedisTimeoutScheduler(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public Task ScheduleAsync(RoomCode code, DateTimeOffset deadline, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var db = _redis.GetDatabase();
        return db.SortedSetAddAsync(
            PlayMe.Infrastructure.Redis.RedisKeys.Timeouts,
            code.Value,
            deadline.ToUnixTimeMilliseconds());
    }

    public Task CancelAsync(RoomCode code, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var db = _redis.GetDatabase();
        return db.SortedSetRemoveAsync(
            PlayMe.Infrastructure.Redis.RedisKeys.Timeouts,
            code.Value);
    }
}
