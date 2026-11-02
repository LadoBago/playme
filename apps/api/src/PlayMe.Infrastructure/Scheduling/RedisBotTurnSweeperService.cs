using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Bots;
using PlayMe.Application.Commands.PlayBotMove;
using PlayMe.Application.Commands.PlayBotSetup;
using PlayMe.Application.Errors;
using PlayMe.Domain.Platform;
using StackExchange.Redis;

namespace PlayMe.Infrastructure.Scheduling;

/// <summary>
/// Sweeper for <c>playme:bot_turns</c> (Sprint 12 seam D; state.md §2.1).
/// Every <see cref="SweeperOptions.ScanInterval"/> it drains due entries,
/// asks <see cref="BotTurnPolicy"/> what the bot owes, dispatches
/// <see cref="PlayBotMoveHandler"/> or <see cref="PlayBotSetupHandler"/>,
/// and broadcasts the outcome through <see cref="IRoomNotifier"/>.
///
/// <para>
/// Unlike the timeout sweeper, the entry is <em>not</em> removed
/// unconditionally after processing: the handlers re-schedule the bot
/// while they run (a retained turn, or play starting right after the
/// bot's setup), which moves the entry's score forward. Removing it
/// blindly would delete that fresh schedule. Instead the sweeper removes
/// the entry only if its score is still the one it read — a Lua
/// compare-and-remove — so a re-schedule survives and a stale entry goes.
/// </para>
///
/// <para>
/// Crash safety: the handlers take the room lock themselves and
/// re-validate the bot's turn under it, so two instances sweeping the
/// same entry both run the module but only the first commit lands; the
/// second is discarded as stale. A crash mid-process leaves the entry
/// in place for the next sweep.
/// </para>
/// </summary>
public sealed partial class RedisBotTurnSweeperService : BackgroundService
{
    // KEYS[1] = sorted set, ARGV[1] = member, ARGV[2] = the score we read.
    private const string RemoveIfScoreUnchanged = """
        local score = redis.call('ZSCORE', KEYS[1], ARGV[1])
        if score and tonumber(score) == tonumber(ARGV[2]) then
            return redis.call('ZREM', KEYS[1], ARGV[1])
        end
        return 0
        """;

    private readonly IConnectionMultiplexer _redis;
    private readonly IServiceScopeFactory _scopes;
    private readonly IRoomRepository _rooms;
    private readonly IRoomNotifier _notifier;
    private readonly IClock _clock;
    private readonly SweeperOptions _options;
    private readonly ILogger<RedisBotTurnSweeperService> _logger;

    public RedisBotTurnSweeperService(
        IConnectionMultiplexer redis,
        IServiceScopeFactory scopes,
        IRoomRepository rooms,
        IRoomNotifier notifier,
        IClock clock,
        IOptions<SweeperOptions> options,
        ILogger<RedisBotTurnSweeperService> logger)
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

    /// <summary>Drain one batch of due entries. Public as a unit-testable seam.</summary>
    public async Task SweepOnceAsync(DateTimeOffset now, CancellationToken ct)
    {
        var db = _redis.GetDatabase();
        var entries = await db.SortedSetRangeByScoreWithScoresAsync(
            PlayMe.Infrastructure.Redis.RedisKeys.BotTurns,
            start: double.NegativeInfinity,
            stop: now.ToUnixTimeMilliseconds(),
            exclude: Exclude.None,
            order: Order.Ascending,
            skip: 0,
            take: _options.BatchSize);

        foreach (var entry in entries)
        {
            if (ct.IsCancellationRequested) return;

            var roomCodeValue = (string?)entry.Element;
            if (string.IsNullOrEmpty(roomCodeValue))
            {
                continue;
            }

            await ProcessEntryAsync(db, roomCodeValue, entry.Score, ct);
        }
    }

    private async Task ProcessEntryAsync(IDatabase db, string roomCodeValue, double score, CancellationToken ct)
    {
        if (!RoomCode.TryCreate(roomCodeValue, out var code))
        {
            await db.SortedSetRemoveAsync(PlayMe.Infrastructure.Redis.RedisKeys.BotTurns, roomCodeValue);
            return;
        }

        var room = await _rooms.LoadAsync(code, ct);
        var action = room is null ? BotAction.None : BotTurnPolicy.PendingAction(room);

        var retry = action switch
        {
            BotAction.Move => await DispatchMoveAsync(code, roomCodeValue, ct),
            BotAction.Setup => await DispatchSetupAsync(code, roomCodeValue, ct),
            _ => false,
        };

        if (retry)
        {
            // The room lock was busy — leave the entry; the next sweep retries.
            return;
        }

        await db.ScriptEvaluateAsync(
            RemoveIfScoreUnchanged,
            new RedisKey[] { PlayMe.Infrastructure.Redis.RedisKeys.BotTurns },
            new RedisValue[] { roomCodeValue, score });
    }

    /// <summary>Returns true when the entry should be retried on the next sweep.</summary>
    private async Task<bool> DispatchMoveAsync(RoomCode code, string roomCodeValue, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<PlayBotMoveHandler>();
        var result = await handler.HandleAsync(new PlayBotMoveCommand(roomCodeValue), ct);

        if (!result.Succeeded)
        {
            if (result.Error == PlatformErrors.RoomBusy) return true;
            LogDispatchFailed(_logger, "move", result.Error!);
            return false;
        }

        var value = result.Value!;
        if (!value.Applied) return false;

        if (!value.TimedOut)
        {
            await _notifier.BroadcastMoveAcceptedAsync(code, value.Room, ct);
        }
        if (value.MatchEnded)
        {
            await _notifier.BroadcastMatchEndedAsync(code, value.Room, ct);
        }
        return false;
    }

    private async Task<bool> DispatchSetupAsync(RoomCode code, string roomCodeValue, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<PlayBotSetupHandler>();
        var result = await handler.HandleAsync(new PlayBotSetupCommand(roomCodeValue), ct);

        if (!result.Succeeded)
        {
            if (result.Error == PlatformErrors.RoomBusy) return true;
            LogDispatchFailed(_logger, "setup", result.Error!);
            return false;
        }

        var value = result.Value!;
        if (!value.Applied) return false;

        if (value.MatchStarted)
        {
            await _notifier.BroadcastMatchStartedAsync(code, value.Room, ct);
        }
        else
        {
            await _notifier.BroadcastOpponentSetupCommittedAsync(
                code, PlayMe.Domain.Platform.Role.Challenger, value.Room, ct);
        }
        return false;
    }

    [LoggerMessage(
        EventId = 2500,
        Level = LogLevel.Information,
        Message = "Bot-turn sweeper started: interval={IntervalMs}ms, batch={BatchSize}")]
    private static partial void LogStarted(ILogger logger, double intervalMs, int batchSize);

    [LoggerMessage(EventId = 2501, Level = LogLevel.Information, Message = "Bot-turn sweeper stopped")]
    private static partial void LogStopped(ILogger logger);

    [LoggerMessage(EventId = 2502, Level = LogLevel.Error, Message = "Bot-turn sweeper iteration failed")]
    private static partial void LogSweepFailed(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 2503,
        Level = LogLevel.Warning,
        Message = "Bot-turn sweeper Redis call timed out; entries remain scheduled and retry next sweep")]
    private static partial void LogSweepTimedOut(ILogger logger, Exception ex);

    [LoggerMessage(
        EventId = 2504,
        Level = LogLevel.Error,
        Message = "Bot {Action} dispatch failed with {Error}; the entry is dropped")]
    private static partial void LogDispatchFailed(ILogger logger, string action, string error);
}
