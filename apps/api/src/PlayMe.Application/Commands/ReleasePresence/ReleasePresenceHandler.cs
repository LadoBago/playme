using PlayMe.Application.Abstractions;
using PlayMe.Application.Dtos;
using PlayMe.Application.Errors;
using PlayMe.Application.Mapping;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.ReleasePresence;

public sealed class ReleasePresenceHandler
{
    private readonly IRoomRepository _rooms;

    public ReleasePresenceHandler(IRoomRepository rooms)
    {
        _rooms = rooms;
    }

    public async Task<AppResult<RoomDto>> HandleAsync(
        ReleasePresenceCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        RoomCode code;
        try { code = new RoomCode(cmd.RoomCode); }
        catch (ArgumentException)
        {
            return AppResult<RoomDto>.Fail(ErrorCode.RoomNotFound);
        }

        try
        {
            return await _rooms.WithLockAsync(code, async () =>
            {
                var room = await _rooms.LoadAsync(code, ct);
                if (room is null)
                {
                    return AppResult<RoomDto>.Fail(ErrorCode.RoomNotFound);
                }

                var stored = room.PlayerFor(cmd.CallerRole);
                // Stale disconnects from a previous session: silently no-op
                // rather than 401 — the room may have already cleaned the seat.
                if (stored is not null && stored.Id.Value == cmd.CallerPlayerId)
                {
                    room.MarkDisconnected(cmd.CallerRole);
                    await _rooms.SaveAsync(room, ct);
                }

                return AppResult<RoomDto>.Ok(RoomMapper.ToDto(room));
            }, ct);
        }
        catch (LockTimeoutException)
        {
            return AppResult<RoomDto>.Fail(ErrorCode.RoomBusy);
        }
    }
}
