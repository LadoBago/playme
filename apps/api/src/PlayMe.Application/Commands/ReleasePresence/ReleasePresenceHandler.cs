using PlayMe.Application.Abandon;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Errors;
using PlayMe.Application.Mapping;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.ReleasePresence;

public sealed class ReleasePresenceHandler
{
    /// <summary>
    /// Post-match reconnect grace (docs/state.md §2.4). Long enough to
    /// cover refresh, locale toggle, and the SignalR auto-reconnect
    /// window; short enough that a genuine close still feels prompt to
    /// the still-connected player. If this needs to differ per
    /// environment, lift to <c>SweeperOptions</c> or a sibling.
    /// </summary>
    public static readonly TimeSpan PostMatchExitGracePeriod = TimeSpan.FromSeconds(10);

    private readonly IRoomRepository _rooms;
    private readonly IClock _clock;
    private readonly IDisconnectGraceScheduler _graces;
    private readonly IPostMatchExitGraceScheduler _postMatchGraces;
    private readonly IGameModuleRegistry _games;

    public ReleasePresenceHandler(
        IRoomRepository rooms,
        IClock clock,
        IDisconnectGraceScheduler graces,
        IPostMatchExitGraceScheduler postMatchGraces,
        IGameModuleRegistry games)
    {
        _rooms = rooms;
        _clock = clock;
        _graces = graces;
        _postMatchGraces = postMatchGraces;
        _games = games;
    }

    public async Task<AppResult<ReleasePresenceResult>> HandleAsync(
        ReleasePresenceCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (!RoomCode.TryCreate(cmd.RoomCode, out var code))
        {
            return AppResult<ReleasePresenceResult>.Fail(PlatformErrors.RoomNotFound);
        }

        try
        {
            return await _rooms.WithLockAsync(code, async () =>
            {
                var room = await _rooms.LoadAsync(code, ct);
                if (room is null)
                {
                    return AppResult<ReleasePresenceResult>.Fail(PlatformErrors.RoomNotFound);
                }

                var stored = room.PlayerFor(cmd.CallerRole);
                // Stale disconnects from a previous session: silently no-op
                // rather than 401 — the room may have already cleaned the seat.
                if (stored is null || stored.Id.Value != cmd.CallerPlayerId)
                {
                    return Effect(room, PresenceReleaseEffect.None);
                }

                // Defensive no-op: a SignalR disconnect for a role that's
                // already marked disconnected mustn't re-broadcast
                // OpponentDisconnected or re-schedule grace. Happens when
                // a stale-cookie probe connects briefly and tears down
                // (e.g. opening a different room's link in the same
                // browser) — the client's hub.stop() triggers
                // OnDisconnectedAsync even though no presence was
                // actually held in this room.
                var wasConnected = cmd.CallerRole switch
                {
                    Role.Host => room.HostConnected,
                    Role.Challenger => room.ChallengerConnected,
                    _ => false,
                };
                if (!wasConnected)
                {
                    return Effect(room, PresenceReleaseEffect.None);
                }

                // Post-match disconnect (Ended / AwaitingRematch): schedule a
                // brief reconnect grace (state.md §2.4) instead of treating
                // the disconnect as an immediate exit. Covers refresh, locale
                // toggle, and transient blips — the post-match UI looks
                // identical before and during the window, so the still-
                // connected player sees nothing until the grace either
                // elapses (sweeper broadcasts OpponentExited + Closed) or is
                // cancelled by a reconnect. An explicit ExitRoom from the
                // disconnected player still closes the room immediately on
                // its own path.
                if (room.Status is RoomStatus.Ended or RoomStatus.AwaitingRematch)
                {
                    room.MarkDisconnected(cmd.CallerRole);
                    await _rooms.SaveAsync(room, ct);
                    var deadline = _clock.UtcNow + PostMatchExitGracePeriod;
                    await _postMatchGraces.ScheduleAsync(code, cmd.CallerRole, deadline, ct);
                    return Effect(room, PresenceReleaseEffect.None);
                }

                var notifyOpponent = room.Status == RoomStatus.InProgress;
                room.MarkDisconnected(cmd.CallerRole);
                await _rooms.SaveAsync(room, ct);

                // Conditional abandon-grace per docs/platform-and-games.md §1 #7:
                // only schedule when (a) it's the disconnected player's turn at
                // the disconnect moment (mirrors the lazy chess clock — no point
                // ticking grace when the game is waiting on the still-connected
                // player), and (b) the disconnected player's effective remaining
                // clock is strictly greater than the grace window (otherwise the
                // chess-clock timeout sweeper already catches the abandon as a
                // Timeout outcome).
                if (notifyOpponent && room.CurrentMatch is not null
                    && room.CurrentMatch.Clock.ActivePlayer == cmd.CallerRole)
                {
                    var module = _games.GetModule(room.GameId);
                    var now = _clock.UtcNow;
                    var remaining = room.CurrentMatch.Clock.EffectiveRemaining(cmd.CallerRole, now);
                    var deadline = GraceSchedulingPolicy.ComputeDeadline(
                        module.DefaultClockBudget, remaining, now);
                    if (deadline is not null)
                    {
                        await _graces.ScheduleAsync(code, cmd.CallerRole, deadline.Value, ct);
                    }
                }

                return Effect(
                    room,
                    notifyOpponent
                        ? PresenceReleaseEffect.OpponentDisconnected
                        : PresenceReleaseEffect.None);
            }, ct);
        }
        catch (LockTimeoutException)
        {
            return AppResult<ReleasePresenceResult>.Fail(PlatformErrors.RoomBusy);
        }
    }

    private AppResult<ReleasePresenceResult> Effect(Room room, PresenceReleaseEffect effect) =>
        AppResult<ReleasePresenceResult>.Ok(
            new ReleasePresenceResult(
                RoomMapper.ToDto(room, _clock.UtcNow, _games),
                effect));
}
