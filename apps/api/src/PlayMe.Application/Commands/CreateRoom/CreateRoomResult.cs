using PlayMe.Application.Dtos;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.CreateRoom;

/// <summary>
/// Output of <see cref="CreateRoomHandler"/>. The API layer uses
/// <see cref="HostPlayerId"/> to mint the signed session cookie
/// (CLAUDE.md §5.4) and never returns it in the response body.
/// </summary>
public sealed record CreateRoomResult(PlayerId HostPlayerId, RoomDto Room);
