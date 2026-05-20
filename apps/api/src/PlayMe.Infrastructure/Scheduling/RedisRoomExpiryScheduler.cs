using PlayMe.Application.Abstractions;
using PlayMe.Domain.Platform;
using StackExchange.Redis;

namespace PlayMe.Infrastructure.Scheduling;

/// <summary>
/// Backs <see cref="IRoomExpiryScheduler"/> with the
/// <c>playme:expires</c> sorted set (see docs/state.md §2.2 and
/// <see cref="Redis.RedisKeys.Expires"/>). One entry per room — ZADD
/// overwrites on re-schedule, ZREM cancels.
///
/// The sorted-set member is <c>{roomCode}|{gameId}</c> per
/// <see cref="ExpiryMemberKey"/>: the gameId rides along so the
/// sweeper can populate <c>room_expired</c> analytics after the
/// underlying room key has elapsed.
/// </summary>
public sealed class RedisRoomExpiryScheduler : IRoomExpiryScheduler
{
    private readonly IConnectionMultiplexer _redis;

    public RedisRoomExpiryScheduler(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public Task ScheduleAsync(
        RoomCode code,
        GameId gameId,
        DateTimeOffset deadline,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var db = _redis.GetDatabase();
        return db.SortedSetAddAsync(
            PlayMe.Infrastructure.Redis.RedisKeys.Expires,
            ExpiryMemberKey.Encode(code, gameId),
            deadline.ToUnixTimeMilliseconds());
    }

    public Task CancelAsync(RoomCode code, GameId gameId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var db = _redis.GetDatabase();
        return db.SortedSetRemoveAsync(
            PlayMe.Infrastructure.Redis.RedisKeys.Expires,
            ExpiryMemberKey.Encode(code, gameId));
    }
}
