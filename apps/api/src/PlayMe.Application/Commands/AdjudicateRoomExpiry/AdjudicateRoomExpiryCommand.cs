namespace PlayMe.Application.Commands.AdjudicateRoomExpiry;

/// <summary>
/// Sweeper-side dispatch when a <c>playme:expires</c> entry deadline
/// elapses. The sweeper has already acquired the per-room distributed
/// lock; this handler re-reads the room state and fires
/// <c>room_expired</c> only when the room is still
/// <c>WaitingForOpponent</c> (or already reaped from Redis). A
/// late-arriving challenger between the schedule and the sweep would
/// have transitioned the room to <c>InProgress</c> — in that case the
/// handler drops silently.
///
/// <paramref name="GameId"/> rides on the command because by the time
/// the sweeper fires, the room's own Redis key has typically already
/// elapsed; the gameId is preserved on the scheduled member key so
/// analytics can still be populated. See
/// <see cref="Infrastructure.Scheduling.ExpiryMemberKey"/>.
/// </summary>
public sealed record AdjudicateRoomExpiryCommand(string RoomCode, string GameId);
