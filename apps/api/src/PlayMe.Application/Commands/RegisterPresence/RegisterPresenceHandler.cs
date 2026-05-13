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

    public RegisterPresenceHandler(
        IRoomRepository rooms,
        IGameModuleRegistry games,
        IClock clock,
        ITimeoutScheduler timeouts,
        IDisconnectGraceScheduler graces)
    {
        _rooms = rooms;
        _games = games;
        _clock = clock;
        _timeouts = timeouts;
        _graces = graces;
    }

    public async Task<AppResult<RegisterPresenceResult>> HandleAsync(
        RegisterPresenceCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        RoomCode code;
        try { code = new RoomCode(cmd.RoomCode); }
        catch (ArgumentException)
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

                var wasInProgress = room.Status == RoomStatus.InProgress;
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
                        module, module.DefaultClockBudget, now);
                }

                await _rooms.SaveAsync(room, ct);

                // Cancel any pending disconnect-grace entry — the caller is
                // back, so they haven't abandoned.
                if (wasInProgress && !wasConnected)
                {
                    await _graces.CancelAsync(code, cmd.CallerRole, ct);
                }

                // Schedule the first timeout check when the match just
                // started. Subsequent timeouts are re-scheduled in
                // SubmitMoveHandler on every accepted move.
                if (matchJustStarted && room.CurrentMatch is not null)
                {
                    await _timeouts.ScheduleAsync(
                        code,
                        room.CurrentMatch.Clock.ActivePlayerDeadline(),
                        ct);
                }

                var reconnected = wasInProgress && !wasConnected;

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
