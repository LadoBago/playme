using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PlayMe.Application.Abstractions;
using PlayMe.Domain.Platform;
using PlayMe.Infrastructure.Json;
using StackExchange.Redis;

namespace PlayMe.Infrastructure.Redis;

/// <summary>
/// Single-JSON-blob room repository (CLAUDE.md §2.8). One Redis key per
/// room, holds the full <see cref="Room"/> aggregate; another key per room
/// holds the distributed lock used by <see cref="WithLockAsync"/> to
/// serialize move processing across API instances.
/// </summary>
public sealed partial class RedisRoomRepository : IRoomRepository
{
    /// <summary>Lock TTL: long enough for any single mutation, short enough
    /// to self-release if an instance dies mid-flight (§2.8: 5 s).</summary>
    private static readonly TimeSpan LockTtl = TimeSpan.FromSeconds(5);

    /// <summary>Cap on acquisition time: ~500 ms per §2.8 (handler default).</summary>
    private static readonly TimeSpan DefaultLockAcquireBudget = TimeSpan.FromMilliseconds(500);

    /// <summary>Backoff between lock attempts. Bounded contention is expected
    /// (turn-based play), so a short busy-wait is the right shape.</summary>
    private static readonly TimeSpan LockRetryDelay = TimeSpan.FromMilliseconds(20);

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisRoomRepository> _logger;
    private readonly JsonSerializerOptions _json;

    public RedisRoomRepository(
        IConnectionMultiplexer redis,
        ILogger<RedisRoomRepository> logger)
    {
        _redis = redis;
        _logger = logger;
        _json = PlayMeJsonOptions.CreateDefault();
    }

    public async Task<Room?> LoadAsync(RoomCode code, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var db = _redis.GetDatabase();
        var json = await db.StringGetAsync(RedisKeys.Room(code.Value));
        if (!json.HasValue)
        {
            return null;
        }

        var record = JsonSerializer.Deserialize<RoomRecord>((string)json!, _json)
            ?? throw new InvalidOperationException(
                $"Room blob for {code} deserialized to null.");
        return RoomMapping.FromRecord(record);
    }

    public async Task SaveAsync(Room room, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var db = _redis.GetDatabase();
        var record = RoomMapping.ToRecord(room);
        var json = JsonSerializer.Serialize(record, _json);
        await db.StringSetAsync(
            RedisKeys.Room(room.Code.Value),
            json,
            expiry: TtlFor(room.Status));
    }

    public async Task<bool> CreateAsync(Room room, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var db = _redis.GetDatabase();
        var record = RoomMapping.ToRecord(room);
        var json = JsonSerializer.Serialize(record, _json);
        return await db.StringSetAsync(
            RedisKeys.Room(room.Code.Value),
            json,
            expiry: TtlFor(room.Status),
            when: When.NotExists);
    }

    public Task<T> WithLockAsync<T>(
        RoomCode code, Func<Task<T>> work, CancellationToken ct) =>
        WithLockAsync(code, DefaultLockAcquireBudget, work, ct);

    public async Task<T> WithLockAsync<T>(
        RoomCode code,
        TimeSpan acquireWait,
        Func<Task<T>> work,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(work);
        ct.ThrowIfCancellationRequested();

        var lockKey = RedisKeys.RoomLock(code.Value);
        var lockToken = NewLockToken();
        var db = _redis.GetDatabase();

        if (!await TryAcquireAsync(db, lockKey, lockToken, acquireWait, ct))
        {
            LogRoomLockAcquireTimeout(_logger, code.Value, acquireWait.TotalMilliseconds);
            throw new LockTimeoutException(code.Value);
        }

        try
        {
            return await work();
        }
        finally
        {
            // Library implements release as a CAS Lua, so we only ever
            // release a lock that's ours. If the lock already expired
            // (work outran the 5 s TTL), the release is a no-op.
            await db.LockReleaseAsync(lockKey, lockToken);
        }
    }

    private static async Task<bool> TryAcquireAsync(
        IDatabase db,
        string lockKey,
        string lockToken,
        TimeSpan acquireWait,
        CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow + acquireWait;
        while (true)
        {
            if (await db.LockTakeAsync(lockKey, lockToken, LockTtl))
            {
                return true;
            }
            if (DateTimeOffset.UtcNow >= deadline)
            {
                return false;
            }
            await Task.Delay(LockRetryDelay, ct);
        }
    }

    private static string NewLockToken()
    {
        // 16 random bytes -> 32-char hex. Cheap and uniquely identifies
        // which API instance/coroutine owns the lock for the CAS release.
        Span<byte> buffer = stackalloc byte[16];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToHexString(buffer);
    }

    /// <summary>
    /// TTL policy from CLAUDE.md §2.8: 30 min while waiting, 1 h while in
    /// progress, 5 min after Ended (terminal cleanup window). Refreshed on
    /// every save so active rooms don't expire mid-match.
    /// </summary>
    private static TimeSpan TtlFor(RoomStatus status) => status switch
    {
        RoomStatus.WaitingForOpponent => TimeSpan.FromMinutes(30),
        RoomStatus.InProgress => TimeSpan.FromHours(1),
        RoomStatus.Ended => TimeSpan.FromMinutes(5),
        RoomStatus.AwaitingRematch => TimeSpan.FromMinutes(5),
        RoomStatus.Closed => TimeSpan.FromMinutes(5),
        RoomStatus.Expired => TimeSpan.FromMinutes(5),
        _ => TimeSpan.FromMinutes(5),
    };

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Warning,
        Message = "RoomLock acquire timeout for {RoomCode} after {BudgetMs}ms")]
    private static partial void LogRoomLockAcquireTimeout(
        ILogger logger, string roomCode, double budgetMs);
}
