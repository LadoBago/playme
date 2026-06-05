using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlayMe.Application;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Commands.AdjudicatePostMatchExitGrace;
using PlayMe.Domain.Platform;
using StackExchange.Redis;
using Role = PlayMe.Domain.Platform.Role;

namespace PlayMe.Infrastructure.Scheduling;

/// <summary>
/// Drains the <c>playme:postmatch_exit</c> sorted set. Each entry encodes
/// <c>{roomCode}:{role}</c> via <see cref="GraceMemberKey"/>; on expiry,
/// the sweeper fires <see cref="AdjudicatePostMatchExitGraceHandler"/>
/// under the room lock and broadcasts <c>OpponentExited</c> to the
/// still-connected player if the handler closed the room (docs/state.md
/// §2.4).
/// </summary>
public sealed partial class RedisPostMatchExitGraceSweeperService : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceScopeFactory _scopes;
    private readonly IRoomRepository _rooms;
    private readonly IRoomNotifier _notifier;
    private readonly IClock _clock;
    private readonly SweeperOptions _options;
    private readonly ILogger<RedisPostMatchExitGraceSweeperService> _logger;

    public RedisPostMatchExitGraceSweeperService(
        IConnectionMultiplexer redis,
        IServiceScopeFactory scopes,
        IRoomRepository rooms,
        IRoomNotifier notifier,
        IClock clock,
        IOptions<SweeperOptions> options,
        ILogger<RedisPostMatchExitGraceSweeperService> logger)
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

    /// <summary>Unit-testable seam (see <see cref="RedisDisconnectGraceSweeperService.SweepOnceAsync"/>).</summary>
    public async Task SweepOnceAsync(DateTimeOffset now, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var entries = await db.SortedSetRangeByScoreAsync(
            PlayMe.Infrastructure.Redis.RedisKeys.PostMatchExit,
            start: double.NegativeInfinity,
            stop: now.ToUnixTimeMilliseconds(),
            exclude: Exclude.None,
            order: Order.Ascending,
            skip: 0,
            take: _options.BatchSize);

        foreach (var entry in entries)
        {
            if (ct.IsCancellationRequested) return;

            var memberValue = (string?)entry;
            if (string.IsNullOrEmpty(memberValue))
            {
                continue;
            }

            await ProcessEntryAsync(db, memberValue, ct);
        }
    }

    private async Task ProcessEntryAsync(IDatabase db, string memberValue, CancellationToken ct)
    {
        if (!GraceMemberKey.TryDecode(memberValue, out var roomCodeValue, out var role))
        {
            await db.SortedSetRemoveAsync(PlayMe.Infrastructure.Redis.RedisKeys.PostMatchExit, memberValue);
            return;
        }

        if (!RoomCode.TryCreate(roomCodeValue, out var code))
        {
            await db.SortedSetRemoveAsync(PlayMe.Infrastructure.Redis.RedisKeys.PostMatchExit, memberValue);
            return;
        }

        AppResult<AdjudicatePostMatchExitGraceResult>? result = null;
        try
        {
            result = await _rooms.WithLockAsync(
                code,
                _options.LockAcquireBudget,
                async () =>
                {
                    using var scope = _scopes.CreateScope();
                    var handler = scope.ServiceProvider
                        .GetRequiredService<AdjudicatePostMatchExitGraceHandler>();
                    return await handler.HandleAsync(
                        new AdjudicatePostMatchExitGraceCommand(roomCodeValue, role),
                        ct);
                },
                ct);
        }
        catch (LockTimeoutException)
        {
            return; // try again next sweep
        }

        await db.SortedSetRemoveAsync(PlayMe.Infrastructure.Redis.RedisKeys.PostMatchExit, memberValue);

        if (result is { Succeeded: true, Value: { Exited: true, Room: { } room, ExitedRole: var exitedRole } })
        {
            await _notifier.BroadcastOpponentExitedAsync(code, exitedRole, room, ct);
        }
    }

    [LoggerMessage(
        EventId = 2200,
        Level = LogLevel.Information,
        Message = "Post-match exit grace sweeper started: interval={IntervalMs}ms, batch={BatchSize}")]
    private static partial void LogStarted(ILogger logger, double intervalMs, int batchSize);

    [LoggerMessage(EventId = 2201, Level = LogLevel.Information, Message = "Post-match exit grace sweeper stopped")]
    private static partial void LogStopped(ILogger logger);

    [LoggerMessage(EventId = 2202, Level = LogLevel.Error, Message = "Post-match exit grace sweeper iteration failed")]
    private static partial void LogSweepFailed(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 2203,
        Level = LogLevel.Warning,
        Message = "Post-match exit grace sweeper Redis call timed out; entries remain scheduled and retry next sweep")]
    private static partial void LogSweepTimedOut(ILogger logger, Exception ex);
}
