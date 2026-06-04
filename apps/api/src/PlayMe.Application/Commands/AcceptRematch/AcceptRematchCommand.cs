using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.AcceptRematch;

/// <summary>
/// Hub <c>AcceptRematch</c> dispatch (docs/platform.md §1 #10).
/// Only the responder may accept; the offerer can't accept their own offer.
/// </summary>
public sealed record AcceptRematchCommand(
    string RoomCode,
    string CallerPlayerId,
    Role CallerRole);
