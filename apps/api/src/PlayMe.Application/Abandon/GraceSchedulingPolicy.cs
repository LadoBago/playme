using PlayMe.Domain.Platform;

namespace PlayMe.Application.Abandon;

/// <summary>
/// Shared policy for the conditional scheduling of disconnect-grace entries
/// (docs/platform-and-games.md §1 #7). Called by:
/// <list type="bullet">
///   <item><see cref="Commands.ReleasePresence.ReleasePresenceHandler"/> at
///   disconnect-moment, when the disconnected role is the active player.</item>
///   <item><see cref="Commands.SubmitMove.SubmitMoveHandler"/> after a turn
///   flip, when the new active player is offline.</item>
/// </list>
///
/// Schedules only when:
/// <list type="number">
///   <item>The configured budget yields a non-null grace tier
///   (<c>PlatformConstants.GraceForBudget</c>).</item>
///   <item>The disconnected player's effective remaining clock at the
///   scheduling moment is strictly greater than the grace window —
///   otherwise the chess-clock timeout fires first and the room ends
///   with <c>Outcome.Timeout</c> on its own.</item>
/// </list>
/// </summary>
public static class GraceSchedulingPolicy
{
    /// <summary>
    /// Compute the absolute deadline at which the grace should fire, or
    /// null if no entry should be scheduled. Callers pass the disconnected
    /// player's remaining clock — <see cref="MatchClock.EffectiveRemaining"/>
    /// at <paramref name="now"/> if they're currently active, or the stored
    /// value (which equals "remaining at start of their next turn") if not.
    /// </summary>
    public static DateTimeOffset? ComputeDeadline(
        TimeSpan budget,
        TimeSpan disconnectedRemaining,
        DateTimeOffset now)
    {
        var grace = PlatformConstants.GraceForBudget(budget);
        if (grace is null) return null;
        if (disconnectedRemaining <= grace.Value) return null;
        return now + grace.Value;
    }
}
