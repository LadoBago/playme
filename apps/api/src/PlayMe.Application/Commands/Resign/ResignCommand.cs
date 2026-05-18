using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.Resign;

/// <summary>
/// Hub <c>Resign</c> dispatch (docs/architecture.md §3.3, docs/platform-and-games.md §1 #8).
/// Identity comes from the signed session cookie (docs/security.md §4); the
/// caller voluntarily ends the in-progress match in their own loss.
/// </summary>
public sealed record ResignCommand(
    string RoomCode,
    string CallerPlayerId,
    Role CallerRole);
