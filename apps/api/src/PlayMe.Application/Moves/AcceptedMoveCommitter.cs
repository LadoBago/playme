using PlayMe.Application.Abandon;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Bots;
using PlayMe.Application.Telemetry;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Moves;

/// <summary>
/// The tail of the move pipeline, shared by the human path
/// (<c>SubmitMoveHandler</c>) and the bot path (<c>PlayBotMoveHandler</c>)
/// so the two cannot drift (Sprint 12 seam D). Everything before this
/// point — who may move, parsing, the module's accept/reject verdict — is
/// the caller's; everything after acceptance — advancing the match,
/// ending it, persisting, re-arming the timeout, the opponent's
/// disconnect grace, analytics, and the bot's next turn — lives here.
/// Must be called inside the room lock.
/// </summary>
public sealed class AcceptedMoveCommitter
{
    private readonly IRoomRepository _rooms;
    private readonly ITimeoutScheduler _timeouts;
    private readonly IDisconnectGraceScheduler _graces;
    private readonly IAnalyticsClient _analytics;
    private readonly BotTurnPlanner _botTurns;

    public AcceptedMoveCommitter(
        IRoomRepository rooms,
        ITimeoutScheduler timeouts,
        IDisconnectGraceScheduler graces,
        IAnalyticsClient analytics,
        BotTurnPlanner botTurns)
    {
        _rooms = rooms;
        _timeouts = timeouts;
        _graces = graces;
        _analytics = analytics;
        _botTurns = botTurns;
    }

    /// <summary>
    /// Commit a move the module has accepted on behalf of
    /// <paramref name="moverRole"/>. Returns true when the move ended the
    /// match.
    /// </summary>
    public async Task<bool> CommitAsync(
        Room room,
        IGameModule module,
        string moverSide,
        Role moverRole,
        MoveResult moveResult,
        DateTimeOffset now,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(room);
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(moveResult);
        if (!moveResult.Accepted || room.CurrentMatch is null)
        {
            throw new InvalidOperationException("Only an accepted move on a live match can be committed.");
        }

        var match = room.CurrentMatch;
        var code = room.Code;

        // Seam B (Sprint 10): the module may retain the mover's turn
        // (e.g. sea battle's hit-shoots-again). MatchClock.AfterMove
        // handles nextActive == ActivePlayer correctly — the mover's
        // elapsed slice is committed and their clock keeps draining.
        var nextSide = moveResult.KeepTurn
            ? moverSide
            : module.OtherSide(moverSide);
        var nextActive = room.RoleForSide(nextSide);
        match.ApplyAcceptedMove(moveResult.NewState!, nextSide, nextActive, now, moveResult.Ending);

        if (moveResult.Ending is not null)
        {
            room.EndCurrentMatch();
            await _rooms.SaveAsync(room, ct);
            await _timeouts.CancelAsync(code, ct);
            // Any pending abandon-grace entry for either role is moot once
            // the match has ended.
            await _graces.CancelAsync(code, moverRole, ct);
            await _graces.CancelAsync(code, nextActive, ct);
            await TrackMatchEndedAsync(room, ct);
            return true;
        }

        await _rooms.SaveAsync(room, ct);
        // The next active player's clock is now ticking (the opponent's,
        // or the mover's again on a retained turn) — re-schedule the
        // timeout check at their new deadline.
        await _timeouts.ScheduleAsync(code, match.Clock.ActivePlayerDeadline(), ct);

        // Abandon-grace tracks the active player's turn (docs/platform.md
        // §1 #7). Drop any grace entry standing against the mover — it was
        // computed for a turn that just ended. If the new active player is
        // offline (on a retained turn that can only be the mover racing a
        // disconnect), the grace timer now starts; schedule if the tier
        // policy allows. A bot is always connected, so this never targets it.
        await _graces.CancelAsync(code, moverRole, ct);

        var newActiveConnected = nextActive switch
        {
            Role.Host => room.HostConnected,
            Role.Challenger => room.ChallengerConnected,
            _ => true,
        };
        if (!newActiveConnected)
        {
            // Their turn just started — their stored remaining is what
            // they have for this turn.
            var remaining = match.Clock.EffectiveRemaining(nextActive, now);
            var deadline = GraceSchedulingPolicy.ComputeDeadline(
                module.ClockBudgetFor(room.GameOptions), remaining, now);
            if (deadline is not null)
            {
                await _graces.ScheduleAsync(code, nextActive, deadline.Value, ct);
            }
        }

        await _botTurns.ScheduleIfPendingAsync(room, now, ct);
        return false;
    }

    /// <summary>
    /// End the match because the side on the move has already run out of
    /// time at <paramref name="now"/> — the caller has verified this via
    /// <see cref="IClockService.HasActivePlayerTimedOut"/>. Used when a
    /// move (human or bot) arrives after the deadline but before the
    /// timeout sweeper fired.
    /// </summary>
    public async Task CommitTimeoutAsync(Room room, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(room);
        var match = room.CurrentMatch
            ?? throw new InvalidOperationException("No live match to time out.");

        match.ApplyTimeout(match.SideToMove, now);
        room.EndCurrentMatch();
        await _rooms.SaveAsync(room, ct);
        await _timeouts.CancelAsync(room.Code, ct);
        await TrackMatchEndedAsync(room, ct);
    }

    /// <summary>
    /// End the match by resignation of <paramref name="resigningSide"/>
    /// outside the Hub's resign flow — the bot path uses it when the
    /// module returns an illegal move (docs/bots.md §2.2: a module bug
    /// forfeits the match, never retries).
    /// </summary>
    public async Task CommitForfeitAsync(Room room, string resigningSide, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(room);
        var match = room.CurrentMatch
            ?? throw new InvalidOperationException("No live match to forfeit.");

        match.Resign(resigningSide);
        room.EndCurrentMatch();
        await _rooms.SaveAsync(room, ct);
        await _timeouts.CancelAsync(room.Code, ct);
        await _graces.CancelAsync(room.Code, Role.Host, ct);
        await _graces.CancelAsync(room.Code, Role.Challenger, ct);
        await TrackMatchEndedAsync(room, ct);
    }

    private Task TrackMatchEndedAsync(Room room, CancellationToken ct) =>
        _analytics.TrackAsync(
            AnalyticsEvents.MatchEnded,
            room.Code.Value,
            AnalyticsEvents.MatchEndedProperties(room.GameId.Value, room.CurrentMatch!.Outcome!),
            ct);
}
