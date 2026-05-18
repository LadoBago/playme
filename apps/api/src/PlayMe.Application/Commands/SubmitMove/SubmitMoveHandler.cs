using PlayMe.Application.Abandon;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Errors;
using PlayMe.Application.Mapping;
using PlayMe.Application.RateLimiting;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.SubmitMove;

/// <summary>
/// The authoritative move pipeline (CLAUDE.md §2.1: "The server is the
/// single source of truth"). Inside the room's distributed lock: authorize
/// the caller, verify the match is in progress and the caller is the active
/// player, recompute the active clock and convert to a timeout if it has
/// already run out, then parse and apply the move via the game module,
/// commit if accepted, advance the clock, and persist.
///
/// The handler is fully game-agnostic — move parsing, rules, and reject
/// vocabulary all come from the per-game module (CLAUDE.md §7 "Platform
/// thinness"). Reject keys flow through opaquely.
/// </summary>
public sealed class SubmitMoveHandler
{
    private readonly IRoomRepository _rooms;
    private readonly IGameModuleRegistry _games;
    private readonly IClock _clock;
    private readonly IClockService _clockService;
    private readonly ITimeoutScheduler _timeouts;
    private readonly IDisconnectGraceScheduler _graces;
    private readonly IRateLimiter _rateLimiter;

    public SubmitMoveHandler(
        IRoomRepository rooms,
        IGameModuleRegistry games,
        IClock clock,
        IClockService clockService,
        ITimeoutScheduler timeouts,
        IDisconnectGraceScheduler graces,
        IRateLimiter rateLimiter)
    {
        _rooms = rooms;
        _games = games;
        _clock = clock;
        _clockService = clockService;
        _timeouts = timeouts;
        _graces = graces;
        _rateLimiter = rateLimiter;
    }

    public async Task<AppResult<SubmitMoveResult>> HandleAsync(
        SubmitMoveCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        RoomCode code;
        try { code = new RoomCode(cmd.RoomCode); }
        catch (ArgumentException)
        {
            return AppResult<SubmitMoveResult>.Fail(PlatformErrors.RoomNotFound);
        }

        // Per-session rate limit before acquiring the room lock so a
        // flood doesn't even reach the contention path (docs/security.md
        // §5: 60 moves/min/session). Keyed by playerId — unique per
        // session, regenerated on each create/join.
        if (!await _rateLimiter.TryAcquireAsync(
                SessionRateLimitPolicies.SubmitMove, cmd.CallerPlayerId, ct))
        {
            return AppResult<SubmitMoveResult>.Fail(PlatformErrors.RateExceeded);
        }

        try
        {
            return await _rooms.WithLockAsync(code, async () =>
            {
                var room = await _rooms.LoadAsync(code, ct);
                if (room is null)
                {
                    return AppResult<SubmitMoveResult>.Fail(PlatformErrors.RoomNotFound);
                }

                var stored = room.PlayerFor(cmd.CallerRole);
                if (stored is null || stored.Id.Value != cmd.CallerPlayerId)
                {
                    return AppResult<SubmitMoveResult>.Fail(PlatformErrors.SessionUnauthorized);
                }

                if (room.Status != RoomStatus.InProgress || room.CurrentMatch is null)
                {
                    return AppResult<SubmitMoveResult>.Fail(PlatformErrors.MoveMatchNotInProgress);
                }

                var match = room.CurrentMatch;
                if (match.IsEnded)
                {
                    return AppResult<SubmitMoveResult>.Fail(PlatformErrors.MoveMatchNotInProgress);
                }

                var now = _clock.UtcNow;

                // Convert a stale-clock move into a timeout before doing
                // anything else: if the caller's clock has already run out,
                // their move is rejected by virtue of having lost on time.
                if (_clockService.HasActivePlayerTimedOut(match.Clock, now))
                {
                    var activeSide = match.SideToMove;
                    match.ApplyTimeout(activeSide, now);
                    room.EndCurrentMatch();
                    await _rooms.SaveAsync(room, ct);
                    await _timeouts.CancelAsync(code, ct);

                    return AppResult<SubmitMoveResult>.Ok(new SubmitMoveResult(
                        Room: RoomMapper.ToDto(room, now, _games),
                        MatchEnded: true,
                        TimedOut: true));
                }

                var callerSide = stored.Side;
                if (callerSide is null || callerSide != match.SideToMove)
                {
                    return AppResult<SubmitMoveResult>.Fail(PlatformErrors.MoveNotYourTurn);
                }

                var parser = _games.GetMoveParser(room.GameId);
                var parseResult = parser.Parse(cmd.Move);
                if (!parseResult.Succeeded)
                {
                    return parseResult.ToFailure<SubmitMoveResult>();
                }

                var module = _games.GetModule(room.GameId);
                var moveResult = module.ApplyMove(match.State, callerSide, parseResult.Value!);
                if (!moveResult.Accepted)
                {
                    return AppResult<SubmitMoveResult>.Fail(moveResult.RejectKey!);
                }

                var nextSide = module.OtherSide(callerSide);
                var nextActive = room.RoleForSide(nextSide);
                match.ApplyAcceptedMove(moveResult.NewState!, nextSide, nextActive, now, moveResult.Ending);

                if (moveResult.Ending is not null)
                {
                    room.EndCurrentMatch();
                    await _rooms.SaveAsync(room, ct);
                    await _timeouts.CancelAsync(code, ct);
                    // Any pending abandon-grace entry for either role is
                    // moot once the match has ended.
                    await _graces.CancelAsync(code, cmd.CallerRole, ct);
                    await _graces.CancelAsync(code, nextActive, ct);
                }
                else
                {
                    await _rooms.SaveAsync(room, ct);
                    // The opponent's clock is now ticking — re-schedule the
                    // timeout check at their new deadline.
                    await _timeouts.ScheduleAsync(
                        code,
                        match.Clock.ActivePlayerDeadline(),
                        ct);

                    // Abandon-grace tracks the active player's turn
                    // (docs/platform-and-games.md §1 #7). The caller (who
                    // just moved) is now inactive — drop any grace entry
                    // standing against them. If the new active player is
                    // offline, the grace timer now starts; schedule if
                    // the tier policy allows.
                    await _graces.CancelAsync(code, cmd.CallerRole, ct);

                    var newActiveConnected = nextActive switch
                    {
                        Role.Host => room.HostConnected,
                        Role.Challenger => room.ChallengerConnected,
                        _ => true,
                    };
                    if (!newActiveConnected)
                    {
                        // Their turn just started — their stored remaining
                        // is what they have for this turn.
                        var remaining = match.Clock.EffectiveRemaining(nextActive, now);
                        var deadline = GraceSchedulingPolicy.ComputeDeadline(
                            module.DefaultClockBudget, remaining, now);
                        if (deadline is not null)
                        {
                            await _graces.ScheduleAsync(code, nextActive, deadline.Value, ct);
                        }
                    }
                }

                return AppResult<SubmitMoveResult>.Ok(new SubmitMoveResult(
                    Room: RoomMapper.ToDto(room, now, _games),
                    MatchEnded: moveResult.Ending is not null,
                    TimedOut: false));
            }, ct);
        }
        catch (LockTimeoutException)
        {
            return AppResult<SubmitMoveResult>.Fail(PlatformErrors.RoomBusy);
        }
    }
}
