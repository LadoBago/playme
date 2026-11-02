namespace PlayMe.Domain.Platform;

/// <summary>
/// A registered player in a room. <see cref="Side"/> is null only while the
/// room is in <see cref="RoomStatus.WaitingForOpponent"/> under
/// <see cref="SideSelectionMode.ChallengerPicks"/>; in every other state both
/// sides are resolved (CLAUDE.md §2.3 #14). <see cref="Kind"/> is
/// <see cref="PlayerKind.Bot"/> only for the virtual challenger of a bot
/// room (Sprint 12 seam D); a bot still carries a <see cref="PlayerId"/>
/// so the room's second-factor checks stay uniform, but no session is ever
/// minted for it.
/// </summary>
public sealed record Player(
    PlayerId Id,
    DisplayName DisplayName,
    string? Side,
    PlayerKind Kind = PlayerKind.Human)
{
    public bool IsBot => Kind == PlayerKind.Bot;
}
