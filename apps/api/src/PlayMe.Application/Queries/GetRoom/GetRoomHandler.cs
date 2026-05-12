using PlayMe.Application.Abstractions;
using PlayMe.Application.Dtos;
using PlayMe.Application.Errors;
using PlayMe.Application.Mapping;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Queries.GetRoom;

public sealed class GetRoomHandler
{
    private readonly IRoomRepository _rooms;

    public GetRoomHandler(IRoomRepository rooms)
    {
        _rooms = rooms;
    }

    public async Task<AppResult<RoomDto>> HandleAsync(
        GetRoomQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        RoomCode code;
        try { code = new RoomCode(query.RoomCode); }
        catch (ArgumentException)
        {
            return AppResult<RoomDto>.Fail(ErrorCode.RoomNotFound);
        }

        var room = await _rooms.LoadAsync(code, ct);
        if (room is null)
        {
            return AppResult<RoomDto>.Fail(ErrorCode.RoomNotFound);
        }

        return AppResult<RoomDto>.Ok(RoomMapper.ToDto(room));
    }
}
