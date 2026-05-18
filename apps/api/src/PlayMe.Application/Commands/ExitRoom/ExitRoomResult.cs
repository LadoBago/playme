using PlayMe.Application.Dtos;

namespace PlayMe.Application.Commands.ExitRoom;

/// <summary>
/// Result of an accepted <c>ExitRoom</c>. <see cref="Transitioned"/> is true
/// when this call moved the room from <see cref="Domain.Platform.RoomStatus.Ended"/>
/// or <see cref="Domain.Platform.RoomStatus.AwaitingRematch"/> to <see cref="Domain.Platform.RoomStatus.Closed"/>;
/// false on the idempotent path (room was already <c>Closed</c>). The Hub
/// only broadcasts <c>OpponentExited</c> when <see cref="Transitioned"/>
/// is true.
/// </summary>
public sealed record ExitRoomResult(RoomDto Room, bool Transitioned);
