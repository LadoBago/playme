namespace PlayMe.Application.Commands.AdjudicateRoomExpiry;

/// <summary>
/// Result of one expiry adjudication. <see cref="Expired"/> is true
/// only when this call actually fired <c>room_expired</c> and
/// <c>RoomExpired</c> — i.e. the room had not been joined by the
/// deadline. False on the "joined late" race where a match started
/// between schedule and sweep.
/// </summary>
public sealed record AdjudicateRoomExpiryResult(bool Expired);
