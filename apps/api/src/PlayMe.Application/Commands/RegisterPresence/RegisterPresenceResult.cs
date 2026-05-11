using PlayMe.Application.Dtos;

namespace PlayMe.Application.Commands.RegisterPresence;

/// <summary>
/// Result of registering presence. <see cref="MatchJustStarted"/> is true on
/// the same call that flipped the room WaitingForOpponent → InProgress, so
/// the Hub knows to broadcast <c>MatchStarted</c>.
/// </summary>
public sealed record RegisterPresenceResult(RoomDto Room, bool MatchJustStarted);
