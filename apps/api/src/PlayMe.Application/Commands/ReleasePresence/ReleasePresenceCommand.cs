using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.ReleasePresence;

/// <summary>
/// Hub <c>OnDisconnectedAsync</c> dispatch. Clears the caller's presence
/// flag. Sprint 1 implements the basic case only (clear-on-disconnect);
/// reconnect grace and <c>OpponentAbandoned</c> arrive in Sprint 2.
/// </summary>
public sealed record ReleasePresenceCommand(
    string RoomCode,
    string CallerPlayerId,
    Role CallerRole);
