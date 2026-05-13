using PlayMe.Domain.Platform;

namespace PlayMe.Application.Abstractions;

/// <summary>
/// Schedule a future timeout check for a room. state.md §2.2: one entry
/// per active room (not a per-room periodic timer); the sweeper picks
/// expired entries up, acquires the room lock, and dispatches the
/// <c>AdjudicateTimeoutHandler</c>.
///
/// Implementation: <c>RedisTimeoutScheduler</c> in Infrastructure (PR #2)
/// backing the <c>playme:timeouts</c> sorted set.
/// </summary>
public interface ITimeoutScheduler
{
    /// <summary>
    /// Schedule (or replace) a single timeout-check entry for
    /// <paramref name="code"/> at <paramref name="deadline"/>. Existing
    /// entries for the same room are overwritten — only one timeout check
    /// is ever outstanding per room.
    /// </summary>
    Task ScheduleAsync(RoomCode code, DateTimeOffset deadline, CancellationToken ct);

    /// <summary>
    /// Cancel any pending timeout check for <paramref name="code"/>. No-op
    /// when no entry exists. Called when a match ends by means other than
    /// timeout (win, draw, resign) so a stale entry doesn't fire later.
    /// </summary>
    Task CancelAsync(RoomCode code, CancellationToken ct);
}
