namespace PlayMe.Domain.Platform;

/// <summary>
/// Platform-wide constants that aren't tied to any single game module.
/// Kept in Domain so both <c>Application</c> handlers and <c>Domain</c>
/// aggregates can consume them without an extra abstraction layer. Per-game
/// values (clock budget, side identifiers, board shape, …) live on the game
/// module itself per CLAUDE.md §7 "Platform thinness".
/// </summary>
public static class PlatformConstants
{
    /// <summary>
    /// Tiered abandon-grace window by per-side clock budget
    /// (docs/platform.md §1 #7). Returns <c>null</c> for very
    /// short games where a grace would be a meaningful fraction of the
    /// clock itself — the chess-clock timeout sweeper catches the abandon
    /// naturally and emits <c>Outcome.Timeout</c> instead.
    ///
    /// Mapping today rests on the budget the module returns from
    /// <see cref="IGameModule.ClockBudgetFor"/> (which may vary by the room's
    /// game options — e.g. Tic-Tac-Toe by board size). When host-selected
    /// time limits land, callers should switch to the room's stored budget;
    /// the tier rule itself stays identical.
    /// </summary>
    public static TimeSpan? GraceForBudget(TimeSpan budget)
    {
        if (budget <= TimeSpan.FromMinutes(1)) return null;
        if (budget <= TimeSpan.FromMinutes(5)) return TimeSpan.FromSeconds(60);
        return TimeSpan.FromSeconds(90);
    }
}
