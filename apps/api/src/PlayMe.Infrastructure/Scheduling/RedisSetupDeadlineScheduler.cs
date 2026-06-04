using PlayMe.Application.Abstractions;
using PlayMe.Domain.Platform;
using StackExchange.Redis;

namespace PlayMe.Infrastructure.Scheduling;

/// <summary>
/// Backs <see cref="ISetupDeadlineScheduler"/> by the
/// <c>playme:setup_deadlines</c> sorted set (Sprint 10 seam C; see
/// RedisKeys.SetupDeadlines). One entry per room — <c>ZADD</c> overwrites
/// any existing score, mirroring <see cref="RedisTimeoutScheduler"/>.
/// </summary>
public sealed class RedisSetupDeadlineScheduler : ISetupDeadlineScheduler
{
    private readonly IConnectionMultiplexer _redis;

    public RedisSetupDeadlineScheduler(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public Task ScheduleAsync(RoomCode code, DateTimeOffset deadline, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var db = _redis.GetDatabase();
        return db.SortedSetAddAsync(
            PlayMe.Infrastructure.Redis.RedisKeys.SetupDeadlines,
            code.Value,
            deadline.ToUnixTimeMilliseconds());
    }

    public Task CancelAsync(RoomCode code, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var db = _redis.GetDatabase();
        return db.SortedSetRemoveAsync(
            PlayMe.Infrastructure.Redis.RedisKeys.SetupDeadlines,
            code.Value);
    }
}
