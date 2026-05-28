using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlayMe.Application;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Commands.AdjudicateDisconnectGrace;
using PlayMe.Domain.Platform;
using StackExchange.Redis;
using Role = PlayMe.Domain.Platform.Role;

namespace PlayMe.Infrastructure.Scheduling;

/// <summary>
/// Mirror of <see cref="RedisTimeoutSweeperService"/> for the
/// <c>playme:grace</c> sorted set. Each entry encodes
/// <c>{roomCode}:{role}</c> via <see cref="GraceMemberKey"/>. Fires the
/// <c>AdjudicateDisconnectGraceHandler</c> under the room lock per
/// docs/platform-and-games.md §1 #7 and broadcasts <c>MatchEnded</c>
/// when the handler ended the match.
/// </summary>
public sealed partial class RedisDisconnectGraceSweeperService : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceScopeFactory _scopes;
    private readonly IRoomRepository _rooms;
    private readonly IRoomNotifier _notifier;
    private readonly IClock _clock;
    private readonly SweeperOptions _options;
    private readonly ILogger<RedisDisconnectGraceSweeperService> _logger;

    public RedisDisconnectGraceSweeperService(
        IConnectionMultiplexer redis,
        IServiceScopeFactory scopes,
        IRoomRepository rooms,
        IRoomNotifier notifier,
        IClock clock,
        IOptions<SweeperOptions> options,
        ILogger<RedisDisconnectGraceSweeperService> logger)
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

    /// <summary>Unit-testable seam (see <see cref="RedisTimeoutSweeperService.SweepOnceAsync"/>).</summary>
    public async Task SweepOnceAsync(DateTimeOffset now, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var entries = await db.SortedSetRangeByScoreAsync(
            PlayMe.Infrastructure.Redis.RedisKeys.Grace,
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
            await db.SortedSetRemoveAsync(PlayMe.Infrastructure.Redis.RedisKeys.Grace, memberValue);
            return;
        }

        if (!RoomCode.TryCreate(roomCodeValue, out var code))
        {
            await db.SortedSetRemoveAsync(PlayMe.Infrastructure.Redis.RedisKeys.Grace, memberValue);
            return;
        }

        AppResult<AdjudicateDisconnectGraceResult>? result = null;
        try
        {
            result = await _rooms.WithLockAsync(
                code,
                _options.LockAcquireBudget,
                async () =>
                {
                    using var scope = _scopes.CreateScope();
                    var handler = scope.ServiceProvider
                        .GetRequiredService<AdjudicateDisconnectGraceHandler>();
                    return await handler.HandleAsync(
                        new AdjudicateDisconnectGraceCommand(roomCodeValue, role),
                        ct);
                },
                ct);
        }
        catch (LockTimeoutException)
        {
            return; // try again next sweep
        }

        await db.SortedSetRemoveAsync(PlayMe.Infrastructure.Redis.RedisKeys.Grace, memberValue);

        if (result is { Succeeded: true, Value: { MatchEnded: true, Room: { } room } })
        {
            await _notifier.BroadcastMatchEndedAsync(code, room, ct);
        }
    }

    [LoggerMessage(
        EventId = 2100,
        Level = LogLevel.Information,
        Message = "Grace sweeper started: interval={IntervalMs}ms, batch={BatchSize}")]
    private static partial void LogStarted(ILogger logger, double intervalMs, int batchSize);

    [LoggerMessage(EventId = 2101, Level = LogLevel.Information, Message = "Grace sweeper stopped")]
    private static partial void LogStopped(ILogger logger);

    [LoggerMessage(EventId = 2102, Level = LogLevel.Error, Message = "Grace sweeper iteration failed")]
    private static partial void LogSweepFailed(ILogger logger, Exception ex);
}
