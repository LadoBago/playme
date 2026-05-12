using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.CreateRoom;

/// <summary>
/// Input for room creation (CLAUDE.md §2.5 configure flow). <c>HostSide</c>
/// is required iff <see cref="SideSelectionMode"/> is
/// <see cref="SideSelectionMode.HostPicksSpecific"/>; under
/// <see cref="SideSelectionMode.Random"/> the server picks for the host;
/// under <see cref="SideSelectionMode.ChallengerPicks"/> sides are unresolved
/// until the challenger registers.
/// </summary>
public sealed record CreateRoomCommand(
    string HostDisplayName,
    string GameId,
    SideSelectionMode SideSelectionMode,
    string? HostSide);
