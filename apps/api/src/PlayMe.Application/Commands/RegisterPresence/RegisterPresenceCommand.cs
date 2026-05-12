using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.RegisterPresence;

/// <summary>
/// Hub <c>JoinRoom</c> dispatch (CLAUDE.md §2.4 RoomHub method index). Marks
/// the caller's SignalR presence as connected. If both players are now
/// registered AND both connected, starts the match per §2.9.
///
/// <c>CallerPlayerId</c> and <c>CallerRole</c> come from the signed session
/// cookie — never from client claims (§5.4).
/// </summary>
public sealed record RegisterPresenceCommand(
    string RoomCode,
    string CallerPlayerId,
    Role CallerRole);
