namespace PlayMe.Domain.Platform;

/// <summary>
/// How sides/colors are assigned at room creation (CLAUDE.md §2.3 #14).
///
/// <list type="bullet">
///   <item><see cref="HostPicksSpecific"/>: host names their side; challenger
///     gets the other. Both sides are resolved at room creation.</item>
///   <item><see cref="Random"/>: server picks the host's side at room
///     creation; challenger gets the other. Both sides are resolved at room
///     creation.</item>
///   <item><see cref="ChallengerPicks"/>: sides remain unresolved until the
///     challenger's join-onboarding step. Both sides are resolved at join.</item>
/// </list>
///
/// In all three modes both sides are fully resolved before the room
/// transitions to <see cref="RoomStatus.InProgress"/>, so platform invariant
/// §2.3 #12 (clock starts immediately when both players are present) holds.
/// </summary>
public enum SideSelectionMode
{
    HostPicksSpecific,
    Random,
    ChallengerPicks,
}
