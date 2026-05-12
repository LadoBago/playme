namespace PlayMe.Domain.Platform;

/// <summary>
/// Room lifecycle states (CLAUDE.md §2.9). Sprint 1 only exercises
/// <see cref="WaitingForOpponent"/>, <see cref="InProgress"/>, and
/// <see cref="Ended"/>; <see cref="AwaitingRematch"/>, <see cref="Closed"/>,
/// and <see cref="Expired"/> are declared up front so later sprints add no
/// new enum values.
/// </summary>
public enum RoomStatus
{
    WaitingForOpponent,
    InProgress,
    Ended,
    AwaitingRematch,
    Closed,
    Expired,
}
