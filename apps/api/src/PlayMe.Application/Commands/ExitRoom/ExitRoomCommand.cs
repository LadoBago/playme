using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.ExitRoom;

/// <summary>
/// Hub <c>ExitRoom</c> dispatch (docs/architecture.md §3.3, docs/state.md §2.4).
/// Caller voluntarily ends the room session from <see cref="RoomStatus.Ended"/>
/// or <see cref="RoomStatus.AwaitingRematch"/> — the room transitions to
/// <see cref="RoomStatus.Closed"/> and the opposite player receives
/// <c>OpponentExited</c>.
/// </summary>
public sealed record ExitRoomCommand(
    string RoomCode,
    string CallerPlayerId,
    Role CallerRole);
