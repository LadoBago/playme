using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.OfferRematch;

/// <summary>
/// Hub <c>OfferRematch</c> dispatch (docs/architecture.md §3.3,
/// docs/platform.md §1 #10). From <c>Ended</c> the call records
/// the offer; from <c>AwaitingRematch</c> with a different role it triggers
/// an implicit accept under the room lock.
/// </summary>
public sealed record OfferRematchCommand(
    string RoomCode,
    string CallerPlayerId,
    Role CallerRole);
