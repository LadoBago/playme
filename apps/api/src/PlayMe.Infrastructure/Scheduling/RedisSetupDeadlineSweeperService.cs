using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlayMe.Application;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Commands.AdjudicateSetupTimeout;
using PlayMe.Domain.Platform;
using StackExchange.Redis;

namespace PlayMe.Infrastructure.Scheduling;

/// <summary>
/// Sweeper for <c>playme:setup_deadlines</c> (Sprint 10 seam C). Mirrors
/// <see cref="RedisTimeoutSweeperService"/>: drains expired entries via
/// <c>ZRANGEBYSCORE … LIMIT 0 N</c>, processes each under the per-room
/// distributed lock through <see cref="AdjudicateSetupTimeoutHandler"/>,
/// and <c>ZREM</c>s the entry whether or not the deadline fired — a fresh
/// entry is scheduled when (and only when) a new setup phase begins.
/// Broadcasts <c>MatchEnded</c> on a forfeit and <c>RoomExpired</c> when
/// neither side committed. Crash safety is identical to the timeout
/// sweeper: lock TTL auto-release + retry on the next sweep.
/// </summary>
public sealed partial class RedisSetupDeadlineSweeperService : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceScopeFactory _scopes;
    private readonly IRoomRepository _rooms;
    private readonly IRoomNotifier _notifier;
    private readonly IClock _clock;
    private readonly SweeperOptions _options;
    private readonly ILogger<RedisSetupDeadlineSweeperService> _logger;

    public RedisSetupDeadlineSweeperService(
        IConnectionMultiplexer redis,
        IServiceScopeFactory scopes,
        IRoomRepository rooms,
        IRoomNotifier notifier,
        IClock clock,
        IOptions<SweeperOptions> options,
        ILogger<RedisSetupDeadlineSweeperService> logger)
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
    /// Drain one batch of expired entries. Public to expose a unit-testable
    /// seam, mirroring the other sweepers.
    /// </summary>
    public async Task SweepOnceAsync(DateTimeOffset now, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var entries = await db.SortedSetRangeByScoreAsync(
            PlayMe.Infrastructure.Redis.RedisKeys.SetupDeadlines,
            start: double.NegativeInfinity,
            stop: now.ToUnixTimeMilliseconds(),
            exclude: Exclude.None,
            order: Order.Ascending,
            skip: 0,
            take: _options.BatchSize);

        foreach (var entry in entries)
        {
            if (ct.IsCancellationRequested) return;

            var roomCodeValue = (string?)entry;
            if (string.IsNullOrEmpty(roomCodeValue))
            {
                continue;
            }

            await ProcessEntryAsync(db, roomCodeValue, ct);
        }
    }

    private async Task ProcessEntryAsync(IDatabase db, string roomCodeValue, CancellationToken ct)
    {
        if (!RoomCode.TryCreate(roomCodeValue, out var code))
        {
            // Garbage member — drop it so we don't loop on it forever.
            await db.SortedSetRemoveAsync(
                PlayMe.Infrastructure.Redis.RedisKeys.SetupDeadlines, roomCodeValue);
            return;
        }

        AppResult<AdjudicateSetupTimeoutResult>? result = null;
        try
        {
            result = await _rooms.WithLockAsync(
                code,
                _options.LockAcquireBudget,
                async () =>
                {
                    using var scope = _scopes.CreateScope();
                    var handler = scope.ServiceProvider
                        .GetRequiredService<AdjudicateSetupTimeoutHandler>();
                    return await handler.HandleAsync(
                        new AdjudicateSetupTimeoutCommand(roomCodeValue), ct);
                },
                ct);
        }
        catch (LockTimeoutException)
        {
            // Another process holds the lock — leave the entry in place;
            // the next sweep retries.
            return;
        }

        // Adjudicated (or dropped as stale) — remove the entry either way.
        await db.SortedSetRemoveAsync(
            PlayMe.Infrastructure.Redis.RedisKeys.SetupDeadlines, roomCodeValue);

        if (result is { Succeeded: true, Value: { } value })
        {
            if (value is { MatchEnded: true, Room: { } room })
            {
                await _notifier.BroadcastMatchEndedAsync(code, room, ct);
            }
            else if (value.Expired)
            {
                await _notifier.BroadcastRoomExpiredAsync(
                    code, RoomExpiryReason.SetupTimeout, ct);
            }
        }
    }

    [LoggerMessage(
        EventId = 2400,
        Level = LogLevel.Information,
        Message = "Setup-deadline sweeper started: interval={IntervalMs}ms, batch={BatchSize}")]
    private static partial void LogStarted(ILogger logger, double intervalMs, int batchSize);

    [LoggerMessage(EventId = 2401, Level = LogLevel.Information, Message = "Setup-deadline sweeper stopped")]
    private static partial void LogStopped(ILogger logger);

    [LoggerMessage(EventId = 2402, Level = LogLevel.Error, Message = "Setup-deadline sweeper iteration failed")]
    private static partial void LogSweepFailed(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 2403,
        Level = LogLevel.Warning,
        Message = "Setup-deadline sweeper Redis call timed out; entries remain scheduled and retry next sweep")]
    private static partial void LogSweepTimedOut(ILogger logger, Exception ex);
}
