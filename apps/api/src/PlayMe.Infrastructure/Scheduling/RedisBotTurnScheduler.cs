using PlayMe.Application.Abstractions;
using PlayMe.Domain.Platform;
using StackExchange.Redis;

namespace PlayMe.Infrastructure.Scheduling;

/// <summary>
/// Backs <see cref="IBotTurnScheduler"/> by the <c>playme:bot_turns</c>
/// sorted set (Sprint 12 seam D; state.md §1). One entry per room —
/// <c>ZADD</c> overwrites any existing score, so re-scheduling on every
/// turn handover is a single round-trip and a retained turn simply moves
/// the entry's due time forward.
/// </summary>
public sealed class RedisBotTurnScheduler : IBotTurnScheduler
{
    private readonly IConnectionMultiplexer _redis;

    public RedisBotTurnScheduler(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public Task ScheduleAsync(RoomCode code, DateTimeOffset dueAt, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var db = _redis.GetDatabase();
        return db.SortedSetAddAsync(
            PlayMe.Infrastructure.Redis.RedisKeys.BotTurns,
            code.Value,
            dueAt.ToUnixTimeMilliseconds());
    }
}
