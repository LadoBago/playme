using PlayMe.Application.Abstractions;
using PlayMe.Application.Errors;
using PlayMe.Application.Mapping;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.ReleasePresence;

public sealed class ReleasePresenceHandler
{
    private readonly IRoomRepository _rooms;
    private readonly IClock _clock;
    private readonly IDisconnectGraceScheduler _graces;

    public ReleasePresenceHandler(
        IRoomRepository rooms,
        IClock clock,
        IDisconnectGraceScheduler graces)
    {
        _rooms = rooms;
        _clock = clock;
        _graces = graces;
    }

    public async Task<AppResult<ReleasePresenceResult>> HandleAsync(
        ReleasePresenceCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        RoomCode code;
        try { code = new RoomCode(cmd.RoomCode); }
        catch (ArgumentException)
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
                    return AppResult<ReleasePresenceResult>.Ok(
                        new ReleasePresenceResult(
                            RoomMapper.ToDto(room, _clock.UtcNow),
                            OpponentNotificationDue: false));
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
                    return AppResult<ReleasePresenceResult>.Ok(
                        new ReleasePresenceResult(
                            RoomMapper.ToDto(room, _clock.UtcNow),
                            OpponentNotificationDue: false));
                }

                var notifyOpponent = room.Status == RoomStatus.InProgress;
                room.MarkDisconnected(cmd.CallerRole);
                await _rooms.SaveAsync(room, ct);

                // Schedule the 30s grace check — Sprint 2 just logs on
                // expiry; Sprint 5 hangs OpponentAbandoned / ClaimVictory
                // off this same path.
                if (notifyOpponent)
                {
                    await _graces.ScheduleAsync(
                        code,
                        cmd.CallerRole,
                        _clock.UtcNow + PlatformConstants.DisconnectGrace,
                        ct);
                }

                return AppResult<ReleasePresenceResult>.Ok(
                    new ReleasePresenceResult(
                        RoomMapper.ToDto(room, _clock.UtcNow),
                        OpponentNotificationDue: notifyOpponent));
            }, ct);
        }
        catch (LockTimeoutException)
        {
            return AppResult<ReleasePresenceResult>.Fail(PlatformErrors.RoomBusy);
        }
    }
}
