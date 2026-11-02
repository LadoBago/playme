using Microsoft.Extensions.Logging;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Bots;
using PlayMe.Application.Errors;
using PlayMe.Application.Mapping;
using PlayMe.Application.Setup;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.PlayBotSetup;

/// <summary>
/// The bot's setup commit for setup games (Sprint 12 seam D; docs/bots.md).
/// Mirrors <c>SubmitSetupHandler</c> with the module's own
/// <see cref="IBotSetup.ChooseSetup"/> in place of a parsed payload, the
/// choice made outside the room lock, and a re-check under it. The
/// validated payload then goes through the shared <see cref="SetupCommitter"/>.
///
/// A module whose setup fails its own <see cref="ISetupGame.ValidateSetup"/>
/// is a bug. There is no forfeit during setup (docs/state.md §2.1: setup
/// expiry never awards a win), so the fault is logged, the bot stays
/// uncommitted, and the setup deadline eventually expires the room.
/// </summary>
public sealed partial class PlayBotSetupHandler
{
    private readonly IRoomRepository _rooms;
    private readonly IGameModuleRegistry _games;
    private readonly IClock _clock;
    private readonly IRandom _random;
    private readonly SetupCommitter _committer;
    private readonly ILogger<PlayBotSetupHandler> _logger;

    public PlayBotSetupHandler(
        IRoomRepository rooms,
        IGameModuleRegistry games,
        IClock clock,
        IRandom random,
        SetupCommitter committer,
        ILogger<PlayBotSetupHandler> logger)
    {
        _rooms = rooms;
        _games = games;
        _clock = clock;
        _random = random;
        _committer = committer;
        _logger = logger;
    }

    public async Task<AppResult<PlayBotSetupResult>> HandleAsync(
        PlayBotSetupCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (!RoomCode.TryCreate(cmd.RoomCode, out var code))
        {
            return AppResult<PlayBotSetupResult>.Fail(PlatformErrors.RoomNotFound);
        }

        // --- Choose outside the lock --------------------------------------
        var snapshot = await _rooms.LoadAsync(code, ct);
        if (snapshot is null)
        {
            return AppResult<PlayBotSetupResult>.Fail(PlatformErrors.RoomNotFound);
        }
        if (BotTurnPolicy.PendingAction(snapshot) != BotAction.Setup)
        {
            return Skipped(snapshot);
        }

        var module = _games.GetModule(snapshot.GameId);
        if (module is not ISetupGame setupGame || module is not IBotSetup botSetup)
        {
            // Same rolling-deploy case as PlayBotMoveHandler: not a fault,
            // leave the entry for an instance whose build has the bot.
            LogModuleNotBotSetupCapableHere(_logger, snapshot.GameId.Value);
            return AppResult<PlayBotSetupResult>.Fail(PlatformErrors.BotNotCapableHere);
        }

        var botSide = snapshot.Challenger!.Side!;
        var level = snapshot.BotLevel!.Value;

        GameMove setup;
        try
        {
            setup = botSetup.ChooseSetup(snapshot.GameOptions, botSide, level, _random);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogChooseSetupThrew(_logger, ex, snapshot.GameId.Value);
            return AppResult<PlayBotSetupResult>.Fail(PlatformErrors.BotModuleFault);
        }

        // --- Commit under the lock ----------------------------------------
        try
        {
            return await _rooms.WithLockAsync(code, async () =>
            {
                var room = await _rooms.LoadAsync(code, ct);
                if (room is null)
                {
                    return AppResult<PlayBotSetupResult>.Fail(PlatformErrors.RoomNotFound);
                }
                if (BotTurnPolicy.PendingAction(room) != BotAction.Setup)
                {
                    return Skipped(room);
                }

                var match = room.CurrentMatch!;
                var rejectKey = setupGame.ValidateSetup(match.State, botSide, setup);
                if (rejectKey is not null)
                {
                    LogInvalidBotSetup(_logger, room.GameId.Value, rejectKey);
                    return AppResult<PlayBotSetupResult>.Fail(PlatformErrors.BotModuleFault);
                }

                var now = _clock.UtcNow;
                var matchStarted = await _committer.CommitAsync(
                    room, setupGame, botSide, Role.Challenger, setup, now, ct);

                return AppResult<PlayBotSetupResult>.Ok(new PlayBotSetupResult(
                    Room: RoomMapper.ToDto(room, now, _games),
                    Applied: true,
                    MatchStarted: matchStarted));
            }, ct);
        }
        catch (LockTimeoutException)
        {
            return AppResult<PlayBotSetupResult>.Fail(PlatformErrors.RoomBusy);
        }
    }

    private AppResult<PlayBotSetupResult> Skipped(Room room) =>
        AppResult<PlayBotSetupResult>.Ok(new PlayBotSetupResult(
            Room: RoomMapper.ToDto(room, _clock.UtcNow, _games),
            Applied: false,
            MatchStarted: false));

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Bot setup due for game {GameId} but this instance's module is not ISetupGame + IBotSetup; leaving it for an instance that is")]
    private static partial void LogModuleNotBotSetupCapableHere(ILogger logger, string gameId);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "IBotSetup.ChooseSetup threw for game {GameId}; the bot stays uncommitted")]
    private static partial void LogChooseSetupThrew(ILogger logger, Exception exception, string gameId);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "IBotSetup.ChooseSetup produced a setup the module rejects for game {GameId} ({RejectKey}); the bot stays uncommitted")]
    private static partial void LogInvalidBotSetup(ILogger logger, string gameId, string rejectKey);
}
