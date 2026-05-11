namespace PlayMe.Application.Commands.JoinRoom;

/// <summary>
/// Challenger registration (CLAUDE.md §2.5 join contract). Atomic — a single
/// API call registers the player and resolves sides where applicable.
/// <c>Side</c> is required iff the room was created with
/// <c>SideSelectionMode.ChallengerPicks</c>; under the other two modes it
/// must be null (the host's choice already determined both sides).
/// </summary>
public sealed record JoinRoomCommand(
    string RoomCode,
    string DisplayName,
    string? Side);
