using PlayMe.Application.Abstractions;
using PlayMe.Application.Errors;
using PlayMe.Application.Mapping;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.RegisterPresence;

public sealed class RegisterPresenceHandler
{
    private readonly IRoomRepository _rooms;
    private readonly IGameModuleRegistry _games;

    public RegisterPresenceHandler(IRoomRepository rooms, IGameModuleRegistry games)
    {
        _rooms = rooms;
        _games = games;
    }

    public async Task<AppResult<RegisterPresenceResult>> HandleAsync(
        RegisterPresenceCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        RoomCode code;
        try { code = new RoomCode(cmd.RoomCode); }
        catch (ArgumentException)
        {
            return AppResult<RegisterPresenceResult>.Fail(ErrorCode.RoomNotFound);
        }

        try
        {
            return await _rooms.WithLockAsync(code, async () =>
            {
                var room = await _rooms.LoadAsync(code, ct);
                if (room is null)
                {
                    return AppResult<RegisterPresenceResult>.Fail(ErrorCode.RoomNotFound);
                }

                var stored = room.PlayerFor(cmd.CallerRole);
                if (stored is null || stored.Id.Value != cmd.CallerPlayerId)
                {
                    return AppResult<RegisterPresenceResult>.Fail(ErrorCode.SessionUnauthorized);
                }

                var wasInProgress = room.Status == RoomStatus.InProgress;
                room.MarkConnected(cmd.CallerRole);

                if (room.Status == RoomStatus.WaitingForOpponent)
                {
                    var module = _games.GetModule(room.GameId);
                    room.TryStartMatch(module);
                }

                var matchJustStarted = !wasInProgress && room.Status == RoomStatus.InProgress;
                await _rooms.SaveAsync(room, ct);

                return AppResult<RegisterPresenceResult>.Ok(
                    new RegisterPresenceResult(
                        RoomMapper.ToDto(room),
                        cmd.CallerRole,
                        matchJustStarted));
            }, ct);
        }
        catch (LockTimeoutException)
        {
            return AppResult<RegisterPresenceResult>.Fail(ErrorCode.RoomBusy);
        }
    }
}
