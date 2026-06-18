using PlayMe.Application.Abstractions;
using PlayMe.Application.Errors;
using PlayMe.Application.Mapping;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.RegisterPresence;

public sealed class RegisterPresenceHandler
{
    private readonly IRoomRepository _rooms;
    private readonly IGameModuleRegistry _games;
    private readonly IClock _clock;
    private readonly ITimeoutScheduler _timeouts;
    private readonly IDisconnectGraceScheduler _graces;
    private readonly IPostMatchExitGraceScheduler _postMatchGraces;
    private readonly IRoomExpiryScheduler _expiry;
    private readonly ISetupDeadlineScheduler _setupDeadlines;

    public RegisterPresenceHandler(
        IRoomRepository rooms,
        IGameModuleRegistry games,
        IClock clock,
        ITimeoutScheduler timeouts,
        IDisconnectGraceScheduler graces,
        IPostMatchExitGraceScheduler postMatchGraces,
        IRoomExpiryScheduler expiry,
        ISetupDeadlineScheduler setupDeadlines)
    {
        _rooms = rooms;
        _games = games;
        _clock = clock;
        _timeouts = timeouts;
        _graces = graces;
        _postMatchGraces = postMatchGraces;
        _expiry = expiry;
        _setupDeadlines = setupDeadlines;
    }

    public async Task<AppResult<RegisterPresenceResult>> HandleAsync(
        RegisterPresenceCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (!RoomCode.TryCreate(cmd.RoomCode, out var code))
        {
            return AppResult<RegisterPresenceResult>.Fail(PlatformErrors.RoomNotFound);
        }

        try
        {
            return await _rooms.WithLockAsync(code, async () =>
            {
                var room = await _rooms.LoadAsync(code, ct);
                if (room is null)
                {
                    return AppResult<RegisterPresenceResult>.Fail(PlatformErrors.RoomNotFound);
                }

                var stored = room.PlayerFor(cmd.CallerRole);
                if (stored is null || stored.Id.Value != cmd.CallerPlayerId)
                {
                    return AppResult<RegisterPresenceResult>.Fail(PlatformErrors.SessionUnauthorized);
                }

                // SettingUp counts as "live" for presence purposes (Sprint 10
                // seam C): disconnects during setup are tracked like in-match
                // disconnects, so reconnects must cancel the pending grace
                // and surface as OpponentReconnected.
                var wasLive = room.Status is RoomStatus.InProgress or RoomStatus.SettingUp;
                var wasPostMatch = room.Status is RoomStatus.Ended or RoomStatus.AwaitingRematch;
                var wasConnected = cmd.CallerRole switch
                {
                    Role.Host => room.HostConnected,
                    Role.Challenger => room.ChallengerConnected,
                    _ => false,
                };
                room.MarkConnected(cmd.CallerRole);

                var now = _clock.UtcNow;
                var matchJustStarted = false;
                if (room.Status == RoomStatus.WaitingForOpponent)
                {
                    var module = _games.GetModule(room.GameId);
                    matchJustStarted = room.TryStartMatch(
                        module, module.ClockBudgetFor(room.GameOptions), now);
                }

                await _rooms.SaveAsync(room, ct);

                // Cancel any pending disconnect-grace entry — the caller is
                // back, so they haven't abandoned.
                if (wasLive && !wasConnected)
                {
                    await _graces.CancelAsync(code, cmd.CallerRole, ct);
                }

                // Cancel any pending post-match exit grace (state.md §2.4).
                // The defensive re-check in the adjudicator catches a race
                // where this cancel arrives after the sweeper already picked
                // up the entry; this cancel just removes it sooner so the
                // sweeper doesn't waste cycles on a no-op.
                if (wasPostMatch && !wasConnected)
                {
                    await _postMatchGraces.CancelAsync(code, cmd.CallerRole, ct);
                }

                // Schedule the appropriate first deadline when the room just
                // left WaitingForOpponent. Setup games (Sprint 10 seam C)
                // enter SettingUp — unclocked, bounded by the module's
                // SetupBudget; everything else starts the chess clock and
                // gets a no-move timeout check (subsequent ones are
                // re-scheduled in SubmitMoveHandler on every accepted move).
                if (matchJustStarted && room.CurrentMatch is not null)
                {
                    if (room.Status == RoomStatus.SettingUp
                        && _games.GetModule(room.GameId) is ISetupGame setupGame)
                    {
                        await _setupDeadlines.ScheduleAsync(
                            code, now + setupGame.SetupBudget, ct);
                    }
                    else
                    {
                        await _timeouts.ScheduleAsync(
                            code,
                            room.CurrentMatch.Clock.ActivePlayerDeadline(),
                            ct);
                    }

                    // Authoritative exit from WaitingForOpponent. Cancel the
                    // unjoined-expiry entry so the sweeper doesn't fire
                    // room_expired for a room that actually made it to
                    // gameplay (or setup).
                    await _expiry.CancelAsync(code, room.GameId, ct);
                }

                var reconnected = wasLive && !wasConnected;

                return AppResult<RegisterPresenceResult>.Ok(
                    new RegisterPresenceResult(
                        RoomMapper.ToDto(room, now, _games),
                        cmd.CallerRole,
                        matchJustStarted,
                        reconnected));
            }, ct);
        }
        catch (LockTimeoutException)
        {
            return AppResult<RegisterPresenceResult>.Fail(PlatformErrors.RoomBusy);
        }
    }
}
