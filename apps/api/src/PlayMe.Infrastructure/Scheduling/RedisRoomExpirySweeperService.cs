using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlayMe.Application;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Commands.AdjudicateRoomExpiry;
using PlayMe.Domain.Platform;
using StackExchange.Redis;

namespace PlayMe.Infrastructure.Scheduling;

/// <summary>
/// state.md §2.2 sweeper for <c>playme:expires</c>. Mirrors the
/// timeout / grace sweepers' structure: every
/// <see cref="SweeperOptions.ScanInterval"/>, drain expired entries
/// via <c>ZRANGEBYSCORE … LIMIT 0 N</c>, process each under the
/// per-room distributed lock, <c>ZREM</c> the entry whether or not it
/// adjudicated.
///
/// Scope: fires <c>room_expired</c> for unjoined rooms whose
/// <see cref="RoomLifetimes.WaitingForOpponent"/> window has elapsed.
/// Cleanup-TTL expiries of terminal-state rooms are not tracked here
/// — they're garbage collection, not a product signal.
///
/// Crash safety: each entry is locked for at most the 5 s room-lock
/// TTL (see <c>RedisRoomRepository</c>). If the sweeper crashes mid-
/// process, the lock auto-releases and the <c>ZREM</c> never happens
/// — the next sweep retries the entry.
/// </summary>
public sealed partial class RedisRoomExpirySweeperService : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceScopeFactory _scopes;
    private readonly IClock _clock;
    private readonly SweeperOptions _options;
    private readonly IRoomRepository _rooms;
    private readonly IRoomNotifier _notifier;
    private readonly ILogger<RedisRoomExpirySweeperService> _logger;

    public RedisRoomExpirySweeperService(
        IConnectionMultiplexer redis,
        IServiceScopeFactory scopes,
        IRoomRepository rooms,
        IRoomNotifier notifier,
        IClock clock,
        IOptions<SweeperOptions> options,
        ILogger<RedisRoomExpirySweeperService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _redis = redis;
        _scopes = scopes;
        _rooms = rooms;
        _notifier = notifier;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(_logger, _options.ScanInterval.TotalMilliseconds, _options.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepOnceAsync(_clock.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (RedisTimeoutException ex)
            {
                // Retryable by design: entries stay in the sorted set and the
                // next tick re-reads them, so a slow Redis round-trip is noise,
                // not an error (Sentry issue 122300482).
                LogSweepTimedOut(_logger, ex);
            }
            catch (Exception ex)
            {
                LogSweepFailed(_logger, ex);
            }

            try
            {
                await Task.Delay(_options.ScanInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        LogStopped(_logger);
    }

    /// <summary>
    /// Drain one batch of expired entries. Public to expose a
    /// unit-testable seam — the loop body has all the interesting logic
    /// (decode member, lock, dispatch handler, ZREM); the wrapping loop
    /// is just a delay.
    /// </summary>
    public async Task SweepOnceAsync(DateTimeOffset now, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var entries = await db.SortedSetRangeByScoreAsync(
            PlayMe.Infrastructure.Redis.RedisKeys.Expires,
            start: double.NegativeInfinity,
            stop: now.ToUnixTimeMilliseconds(),
            exclude: Exclude.None,
            order: Order.Ascending,
            skip: 0,
            take: _options.BatchSize);

        foreach (var entry in entries)
        {
            if (ct.IsCancellationRequested) return;

            var member = (string?)entry;
            if (string.IsNullOrEmpty(member))
            {
                continue;
            }

            await ProcessEntryAsync(db, member, ct);
        }
    }

    private async Task ProcessEntryAsync(IDatabase db, string member, CancellationToken ct)
    {
        if (!ExpiryMemberKey.TryDecode(member, out var roomCodeValue, out var gameIdValue))
        {
            // Garbage / legacy member — drop so we don't loop forever.
            await db.SortedSetRemoveAsync(PlayMe.Infrastructure.Redis.RedisKeys.Expires, member);
            return;
        }

        if (!RoomCode.TryCreate(roomCodeValue, out var code))
        {
            await db.SortedSetRemoveAsync(PlayMe.Infrastructure.Redis.RedisKeys.Expires, member);
            return;
        }

        AppResult<AdjudicateRoomExpiryResult>? result = null;
        try
        {
            result = await _rooms.WithLockAsync(
                code,
                _options.LockAcquireBudget,
                async () =>
                {
                    using var scope = _scopes.CreateScope();
                    var handler = scope.ServiceProvider
                        .GetRequiredService<AdjudicateRoomExpiryHandler>();
                    return await handler.HandleAsync(
                        new AdjudicateRoomExpiryCommand(roomCodeValue, gameIdValue), ct);
                },
                ct);
        }
        catch (LockTimeoutException)
        {
            // Another process holds the lock — leave the entry in place;
            // the next sweep retries.
            return;
        }

        // Adjudication has run (or returned an "ignore" result).
        // ZREM the entry regardless — a new schedule, if needed, would
        // be set by CreateRoomHandler on the next room creation.
        await db.SortedSetRemoveAsync(PlayMe.Infrastructure.Redis.RedisKeys.Expires, member);

        // Broadcast on actual expiry only — the joined-late race returns
        // Expired: false and we skip the network fan-out. Same handler/
        // sweeper split as RedisTimeoutSweeperService line 155.
        if (result is { Succeeded: true, Value.Expired: true })
        {
            await _notifier.BroadcastRoomExpiredAsync(code, RoomExpiryReason.Unjoined, ct);
        }
    }

    [LoggerMessage(
        EventId = 2300,
        Level = LogLevel.Information,
        Message = "Room-expiry sweeper started: interval={IntervalMs}ms, batch={BatchSize}")]
    private static partial void LogStarted(ILogger logger, double intervalMs, int batchSize);

    [LoggerMessage(EventId = 2301, Level = LogLevel.Information, Message = "Room-expiry sweeper stopped")]
    private static partial void LogStopped(ILogger logger);

    [LoggerMessage(EventId = 2302, Level = LogLevel.Error, Message = "Room-expiry sweeper iteration failed")]
    private static partial void LogSweepFailed(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 2303,
        Level = LogLevel.Warning,
        Message = "Room-expiry sweeper Redis call timed out; entries remain scheduled and retry next sweep")]
    private static partial void LogSweepTimedOut(ILogger logger, Exception ex);
}
