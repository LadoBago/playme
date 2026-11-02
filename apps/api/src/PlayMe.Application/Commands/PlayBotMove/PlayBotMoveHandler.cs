using Microsoft.Extensions.Logging;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Bots;
using PlayMe.Application.Errors;
using PlayMe.Application.Mapping;
using PlayMe.Application.Moves;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.PlayBotMove;

/// <summary>
/// The bot's move pipeline (Sprint 12 seam D; docs/bots.md). Mirrors
/// <c>SubmitMoveHandler</c> with the session, rate-limit, and parse steps
/// replaced by the module's own <see cref="IBotPlayer.ChooseMove"/>, and
/// with the search run <em>outside</em> the room lock:
/// <list type="number">
///   <item>Load the room without the lock; bail if the bot owes no move.</item>
///   <item>Project the state for the bot's side when the module hides
///   information (<see cref="IHiddenStateGame"/>), then ask the module for
///   a move. Remember the move count the decision was made against.</item>
///   <item>Under the lock: reload, re-check that the bot still owes a move
///   on the same move count, convert to a timeout if the bot's clock has
///   run out, apply the move through the module, and hand acceptance to
///   the shared <see cref="AcceptedMoveCommitter"/>.</item>
/// </list>
/// An illegal or throwing module is a bug, not a retry: the bot forfeits
/// by resignation and the fault is logged.
/// </summary>
public sealed partial class PlayBotMoveHandler
{
    private readonly IRoomRepository _rooms;
    private readonly IGameModuleRegistry _games;
    private readonly IClock _clock;
    private readonly IClockService _clockService;
    private readonly IRandom _random;
    private readonly AcceptedMoveCommitter _committer;
    private readonly ILogger<PlayBotMoveHandler> _logger;

    public PlayBotMoveHandler(
        IRoomRepository rooms,
        IGameModuleRegistry games,
        IClock clock,
        IClockService clockService,
        IRandom random,
        AcceptedMoveCommitter committer,
        ILogger<PlayBotMoveHandler> logger)
    {
        _rooms = rooms;
        _games = games;
        _clock = clock;
        _clockService = clockService;
        _random = random;
        _committer = committer;
        _logger = logger;
    }

    public async Task<AppResult<PlayBotMoveResult>> HandleAsync(
        PlayBotMoveCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (!RoomCode.TryCreate(cmd.RoomCode, out var code))
        {
            return AppResult<PlayBotMoveResult>.Fail(PlatformErrors.RoomNotFound);
        }

        // --- Think outside the lock ---------------------------------------
        var snapshot = await _rooms.LoadAsync(code, ct);
        if (snapshot is null)
        {
            return AppResult<PlayBotMoveResult>.Fail(PlatformErrors.RoomNotFound);
        }
        if (BotTurnPolicy.PendingAction(snapshot) != BotAction.Move)
        {
            return Skipped(snapshot);
        }

        var module = _games.GetModule(snapshot.GameId);
        if (module is not IBotPlayer bot)
        {
            // A bot room exists only for bot-capable modules (CreateRoom
            // checks); reaching here means the catalog changed under a
            // live room. Nothing sane to do but leave the match alone.
            LogModuleNotBotCapable(_logger, snapshot.GameId.Value);
            return AppResult<PlayBotMoveResult>.Fail(PlatformErrors.BotModuleFault);
        }

        var thinkingMatch = snapshot.CurrentMatch!;
        var botSide = snapshot.Challenger!.Side!;
        var level = snapshot.BotLevel!.Value;
        var decidedAtMoveCount = thinkingMatch.MoveCount;

        // Hidden-state games: the bot sees exactly what its human opponent
        // would see from that seat — honest by construction (docs/bots.md
        // §2.1).
        var visibleState = module is IHiddenStateGame hidden
            ? module.Deserialize(hidden.SerializeFor(thinkingMatch.State, botSide))
            : thinkingMatch.State;

        GameMove? chosen = null;
        try
        {
            chosen = bot.ChooseMove(visibleState, botSide, level, _random);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogChooseMoveThrew(_logger, ex, snapshot.GameId.Value);
        }

        // --- Commit under the lock ----------------------------------------
        try
        {
            return await _rooms.WithLockAsync(code, async () =>
            {
                var room = await _rooms.LoadAsync(code, ct);
                if (room is null)
                {
                    return AppResult<PlayBotMoveResult>.Fail(PlatformErrors.RoomNotFound);
                }

                // The world may have moved while the bot was thinking: the
                // human resigned, timed out, exited, or (on a retained turn
                // race) another bot entry already applied. Any of those
                // makes this decision stale.
                if (BotTurnPolicy.PendingAction(room) != BotAction.Move
                    || room.CurrentMatch!.MoveCount != decidedAtMoveCount)
                {
                    return Skipped(room);
                }

                var match = room.CurrentMatch;
                var now = _clock.UtcNow;

                if (_clockService.HasActivePlayerTimedOut(match.Clock, now))
                {
                    await _committer.CommitTimeoutAsync(room, now, ct);
                    return AppResult<PlayBotMoveResult>.Ok(new PlayBotMoveResult(
                        Room: RoomMapper.ToDto(room, now, _games),
                        Applied: true,
                        MatchEnded: true,
                        TimedOut: true,
                        Forfeited: false));
                }

                var moveResult = chosen is null ? null : module.ApplyMove(match.State, botSide, chosen);
                if (moveResult is not { Accepted: true })
                {
                    LogIllegalBotMove(_logger, room.GameId.Value, moveResult?.RejectKey);
                    await _committer.CommitForfeitAsync(room, botSide, ct);
                    return AppResult<PlayBotMoveResult>.Ok(new PlayBotMoveResult(
                        Room: RoomMapper.ToDto(room, now, _games),
                        Applied: true,
                        MatchEnded: true,
                        TimedOut: false,
                        Forfeited: true));
                }

                var matchEnded = await _committer.CommitAsync(
                    room, module, botSide, Role.Challenger, moveResult, now, ct);

                return AppResult<PlayBotMoveResult>.Ok(new PlayBotMoveResult(
                    Room: RoomMapper.ToDto(room, now, _games),
                    Applied: true,
                    MatchEnded: matchEnded,
                    TimedOut: false,
                    Forfeited: false));
            }, ct);
        }
        catch (LockTimeoutException)
        {
            return AppResult<PlayBotMoveResult>.Fail(PlatformErrors.RoomBusy);
        }
    }

    private AppResult<PlayBotMoveResult> Skipped(Room room) =>
        AppResult<PlayBotMoveResult>.Ok(new PlayBotMoveResult(
            Room: RoomMapper.ToDto(room, _clock.UtcNow, _games),
            Applied: false,
            MatchEnded: false,
            TimedOut: false,
            Forfeited: false));

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Bot room for game {GameId} but the module is not an IBotPlayer; leaving the match alone")]
    private static partial void LogModuleNotBotCapable(ILogger logger, string gameId);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "IBotPlayer.ChooseMove threw for game {GameId}; the bot will forfeit")]
    private static partial void LogChooseMoveThrew(ILogger logger, Exception exception, string gameId);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "IBotPlayer.ChooseMove returned an illegal move for game {GameId} (reject {RejectKey}); the bot forfeits")]
    private static partial void LogIllegalBotMove(ILogger logger, string gameId, string? rejectKey);
}
