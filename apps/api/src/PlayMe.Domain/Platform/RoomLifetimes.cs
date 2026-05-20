namespace PlayMe.Domain.Platform;

/// <summary>
/// Platform-level TTL policy (docs/state.md §1, CLAUDE.md §2.8). Defines
/// how long a room's Redis backing lives in each status before idle
/// cleanup. Centralised so the repository (which sets the TTL on every
/// save) and the expiry sweeper (which fires <c>room_expired</c>) use a
/// single source of truth.
/// </summary>
public static class RoomLifetimes
{
    /// <summary>
    /// Window for the host to recruit a challenger. After this, the room
    /// is reaped from Redis and the sweeper fires <c>room_expired</c>.
    /// </summary>
    public static readonly TimeSpan WaitingForOpponent = TimeSpan.FromMinutes(30);
}
