using PlayMe.Domain.Platform;

namespace PlayMe.Application.Abstractions;

/// <summary>
/// Pure-compute facade over <see cref="MatchClock"/> for handlers. Wraps the
/// lazy-snapshot arithmetic from state.md §2.2 so callers don't recompute
/// it ad-hoc. No I/O, no external dependencies — handlers can pass any
/// clock snapshot in.
/// </summary>
public interface IClockService
{
    /// <summary>
    /// Effective remaining time for <paramref name="role"/> in
    /// <paramref name="snapshot"/> at the given <paramref name="now"/>.
    /// Floors at zero.
    /// </summary>
    TimeSpan Remaining(MatchClock snapshot, Role role, DateTimeOffset now);

    /// <summary>
    /// True when the active player's effective remaining time at
    /// <paramref name="now"/> is zero — i.e. they have already lost on
    /// time but the timeout hasn't been adjudicated yet.
    /// </summary>
    bool HasActivePlayerTimedOut(MatchClock snapshot, DateTimeOffset now);
}
