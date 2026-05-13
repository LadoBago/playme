using PlayMe.Application.Dtos;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.RegisterPresence;

/// <summary>
/// Result of registering presence. <see cref="MatchJustStarted"/> is true on
/// the same call that flipped the room WaitingForOpponent → InProgress, so
/// the Hub knows to broadcast <c>MatchStarted</c>. <see cref="Reconnected"/>
/// is true when the caller was disconnected before this call landed (room
/// already InProgress) — the Hub uses it to broadcast <c>OpponentReconnected</c>
/// to the other player. <see cref="CallerRole"/> echoes the role the server
/// authorized the caller for (from the session cookie), so the Hub can
/// return it to the client without the client ever having to guess.
/// </summary>
public sealed record RegisterPresenceResult(
    RoomDto Room,
    Role CallerRole,
    bool MatchJustStarted,
    bool Reconnected);
