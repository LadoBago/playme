using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlayMe.Application;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Commands.AdjudicateTimeout;
using PlayMe.Domain.Platform;
using StackExchange.Redis;

namespace PlayMe.Infrastructure.Scheduling;

/// <summary>
/// state.md §2.2 sweeper for <c>playme:timeouts</c>. Every
/// <see cref="SweeperOptions.ScanInterval"/>, drains expired entries via
/// <c>ZRANGEBYSCORE … LIMIT 0 N</c>, processes each one under the
/// per-room distributed lock, and removes it via <c>ZREM</c> — whether or
/// not the timeout actually fired (the entry has been adjudicated and a
/// new one will be scheduled by <c>SubmitMoveHandler</c> on the next
/// accepted move).
///
/// Crash safety: each entry is locked for at most the 5 s room-lock TTL
/// (see <c>RedisRoomRepository</c>). If the sweeper crashes mid-process,
/// the lock auto-releases and the <c>ZREM</c> never happens — the next
/// sweep retries the entry.
/// </summary>
public sealed partial class RedisTimeoutSweeperService : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceScopeFactory _scopes;
    private readonly IRoomRepository _rooms;
    private readonly IRoomNotifier _notifier;
    private readonly IClock _clock;
    private readonly SweeperOptions _options;
    private readonly ILogger<RedisTimeoutSweeperService> _logger;

    public RedisTimeoutSweeperService(
        IConnectionMultiplexer redis,
        IServiceScopeFactory scopes,
        IRoomRepository rooms,
        IRoomNotifier notifier,
        IClock clock,
        IOptions<SweeperOptions> options,
        ILogger<RedisTimeoutSweeperService> logger)
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
    /// seam — the loop body has all the interesting logic; the wrapping
    /// loop is just a delay.
    /// </summary>
    public async Task SweepOnceAsync(DateTimeOffset now, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var entries = await db.SortedSetRangeByScoreAsync(
            PlayMe.Infrastructure.Redis.RedisKeys.Timeouts,
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
        RoomCode code;
        try { code = new RoomCode(roomCodeValue); }
        catch (ArgumentException)
        {
            // Garbage member — drop it so we don't loop on it forever.
            await db.SortedSetRemoveAsync(PlayMe.Infrastructure.Redis.RedisKeys.Timeouts, roomCodeValue);
            return;
        }

        AppResult<AdjudicateTimeoutResult>? result = null;
        try
        {
            result = await _rooms.WithLockAsync(
                code,
                _options.LockAcquireBudget,
                async () =>
                {
                    using var scope = _scopes.CreateScope();
                    var handler = scope.ServiceProvider.GetRequiredService<AdjudicateTimeoutHandler>();
                    return await handler.HandleAsync(new AdjudicateTimeoutCommand(roomCodeValue), ct);
                },
                ct);
        }
        catch (LockTimeoutException)
        {
            // Another process holds the lock — leave the entry in place;
            // the next sweep retries.
            return;
        }

        // Adjudication has run (or returned an "ignore" result). Remove the
        // entry now — a new schedule, if needed, is set by SubmitMoveHandler.
        await db.SortedSetRemoveAsync(PlayMe.Infrastructure.Redis.RedisKeys.Timeouts, roomCodeValue);

        if (result is { Succeeded: true, Value: { TimedOut: true, Room: { } room } })
        {
            await _notifier.BroadcastMatchEndedAsync(code, room, ct);
        }
    }

    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Information,
        Message = "Timeout sweeper started: interval={IntervalMs}ms, batch={BatchSize}")]
    private static partial void LogStarted(ILogger logger, double intervalMs, int batchSize);

    [LoggerMessage(EventId = 2001, Level = LogLevel.Information, Message = "Timeout sweeper stopped")]
    private static partial void LogStopped(ILogger logger);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Error, Message = "Timeout sweeper iteration failed")]
    private static partial void LogSweepFailed(ILogger logger, Exception ex);
}
