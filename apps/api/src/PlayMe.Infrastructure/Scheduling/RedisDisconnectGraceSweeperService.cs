using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Commands.AdjudicateDisconnectGrace;
using PlayMe.Domain.Platform;
using StackExchange.Redis;
using Role = PlayMe.Domain.Platform.Role;

namespace PlayMe.Infrastructure.Scheduling;

/// <summary>
/// Mirror of <see cref="RedisTimeoutSweeperService"/> for the
/// <c>playme:grace</c> sorted set. Each entry encodes
/// <c>{roomCode}:{role}</c> via <see cref="GraceMemberKey"/>. The
/// Sprint 2 consumer (<c>AdjudicateDisconnectGraceHandler</c>) is a stub
/// that logs only — Sprint 5 will replace it with the
/// <c>OpponentAbandoned</c> emit and the <c>ClaimVictory</c> unlock.
/// Wiring is in place now so Sprint 5 is a pure addition.
/// </summary>
public sealed partial class RedisDisconnectGraceSweeperService : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceScopeFactory _scopes;
    private readonly IRoomRepository _rooms;
    private readonly SweeperOptions _options;
    private readonly ILogger<RedisDisconnectGraceSweeperService> _logger;

    public RedisDisconnectGraceSweeperService(
        IConnectionMultiplexer redis,
        IServiceScopeFactory scopes,
        IRoomRepository rooms,
        IOptions<SweeperOptions> options,
        ILogger<RedisDisconnectGraceSweeperService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _redis = redis;
        _scopes = scopes;
        _rooms = rooms;
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
                await SweepOnceAsync(DateTimeOffset.UtcNow, stoppingToken);
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

        RoomCode code;
        try { code = new RoomCode(roomCodeValue); }
        catch (ArgumentException)
        {
            await db.SortedSetRemoveAsync(PlayMe.Infrastructure.Redis.RedisKeys.Grace, memberValue);
            return;
        }

        try
        {
            await _rooms.WithLockAsync(
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
