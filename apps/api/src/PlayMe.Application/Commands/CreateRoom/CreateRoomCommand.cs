using System.Text.Json;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.CreateRoom;

/// <summary>
/// Input for room creation (CLAUDE.md §2.5 configure flow). <c>HostSide</c>
/// is required iff <see cref="SideSelectionMode"/> is
/// <see cref="SideSelectionMode.HostPicksSpecific"/>; under
/// <see cref="SideSelectionMode.Random"/> the server picks for the host;
/// under <see cref="SideSelectionMode.ChallengerPicks"/> sides are unresolved
/// until the challenger registers.
/// <para>
/// <see cref="GameOptions"/> (Sprint 9 PR1) is an opaque per-game options
/// blob — the platform never inspects its shape; the game module owns the
/// schema and validates via <see cref="IGameModule.ValidateOptions"/>. Null
/// for games that don't take options.
/// </para>
/// </summary>
public sealed record CreateRoomCommand(
    string HostDisplayName,
    string GameId,
    SideSelectionMode SideSelectionMode,
    string? HostSide,
    JsonElement? GameOptions = null);
