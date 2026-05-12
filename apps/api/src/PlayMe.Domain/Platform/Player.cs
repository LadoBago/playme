namespace PlayMe.Domain.Platform;

/// <summary>
/// A registered player in a room. <see cref="Side"/> is null only while the
/// room is in <see cref="RoomStatus.WaitingForOpponent"/> under
/// <see cref="SideSelectionMode.ChallengerPicks"/>; in every other state both
/// sides are resolved (CLAUDE.md §2.3 #14).
/// </summary>
public sealed record Player(PlayerId Id, DisplayName DisplayName, string? Side);
