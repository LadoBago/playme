using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.RejectRematch;

/// <summary>
/// Hub <c>RejectRematch</c> dispatch (docs/platform-and-games.md §1 #10).
/// Only the responder may reject; closes the room. The rejector's UI
/// auto-routes to the lobby; the offerer sees <c>RematchDeclined</c> and
/// stays with a manual exit.
/// </summary>
public sealed record RejectRematchCommand(
    string RoomCode,
    string CallerPlayerId,
    Role CallerRole);
