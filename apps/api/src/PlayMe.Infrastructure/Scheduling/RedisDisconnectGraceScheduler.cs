using PlayMe.Application.Abstractions;
using PlayMe.Domain.Platform;
using StackExchange.Redis;
using Role = PlayMe.Domain.Platform.Role;

namespace PlayMe.Infrastructure.Scheduling;

/// <summary>
/// Backs <see cref="IDisconnectGraceScheduler"/> by the <c>playme:grace</c>
/// sorted set. Members are <c>{roomCode}:{role}</c> so each player can
/// have at most one outstanding grace entry per room. <c>ZADD</c> replaces
/// the score; <c>ZREM</c> cancels.
/// </summary>
public sealed class RedisDisconnectGraceScheduler : IDisconnectGraceScheduler
{
    private readonly IConnectionMultiplexer _redis;

    public RedisDisconnectGraceScheduler(IConnectionMultiplexer redis)
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
            PlayMe.Infrastructure.Redis.RedisKeys.Grace,
            GraceMemberKey.Encode(code, role),
            deadline.ToUnixTimeMilliseconds());
    }

    public Task CancelAsync(RoomCode code, Role role, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var db = _redis.GetDatabase();
        return db.SortedSetRemoveAsync(
            PlayMe.Infrastructure.Redis.RedisKeys.Grace,
            GraceMemberKey.Encode(code, role));
    }
}
