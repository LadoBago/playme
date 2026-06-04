namespace PlayMe.Domain.Platform;

/// <summary>
/// Room lifecycle states (CLAUDE.md §2.9; docs/state.md §2).
/// <see cref="SettingUp"/> (Sprint 10 seam C) sits between
/// <see cref="WaitingForOpponent"/> and <see cref="InProgress"/> for games
/// whose module implements <see cref="ISetupGame"/>; setup-less games skip
/// it. Values serialize by name (camelCase string) in both Redis and the
/// wire, so enum order is cosmetic.
/// </summary>
public enum RoomStatus
{
    WaitingForOpponent,
    SettingUp,
    InProgress,
    Ended,
    AwaitingRematch,
    Closed,
    Expired,
}
