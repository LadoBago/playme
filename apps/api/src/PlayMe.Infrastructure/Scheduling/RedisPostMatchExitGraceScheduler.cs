using PlayMe.Application.Abstractions;
using PlayMe.Domain.Platform;
using StackExchange.Redis;
using Role = PlayMe.Domain.Platform.Role;

namespace PlayMe.Infrastructure.Scheduling;

/// <summary>
/// Backs <see cref="IPostMatchExitGraceScheduler"/> by the
/// <c>playme:postmatch_exit</c> sorted set. Members reuse
/// <see cref="GraceMemberKey"/> (<c>{roomCode}:{role}</c>) so each player
/// has at most one outstanding entry per room. <c>ZADD</c> replaces the
/// score; <c>ZREM</c> cancels.
/// </summary>
public sealed class RedisPostMatchExitGraceScheduler : IPostMatchExitGraceScheduler
{
    private readonly IConnectionMultiplexer _redis;

    public RedisPostMatchExitGraceScheduler(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public Task ScheduleAsync(
        RoomCode code,
        Role role,
        DateTimeOffset deadline,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var db = _redis.GetDatabase();
        return db.SortedSetAddAsync(
            PlayMe.Infrastructure.Redis.RedisKeys.PostMatchExit,
            GraceMemberKey.Encode(code, role),
            deadline.ToUnixTimeMilliseconds());
    }

    public Task CancelAsync(RoomCode code, Role role, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var db = _redis.GetDatabase();
        return db.SortedSetRemoveAsync(
            PlayMe.Infrastructure.Redis.RedisKeys.PostMatchExit,
            GraceMemberKey.Encode(code, role));
    }
}
