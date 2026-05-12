using PlayMe.Application.Abstractions;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Time;

/// <summary>
/// Default <see cref="IClockService"/> — thin pure-compute delegation to
/// <see cref="MatchClock"/>. Lives in Application (not Infrastructure)
/// because it has no external dependencies and is trivially testable.
/// </summary>
public sealed class ClockService : IClockService
{
    public TimeSpan Remaining(MatchClock snapshot, Role role, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return snapshot.EffectiveRemaining(role, now);
    }

    public bool HasActivePlayerTimedOut(MatchClock snapshot, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return snapshot.EffectiveRemaining(snapshot.ActivePlayer, now) <= TimeSpan.Zero;
    }
}
