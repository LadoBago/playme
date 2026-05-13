namespace PlayMe.Domain.Platform;

/// <summary>
/// Platform-wide constants that aren't tied to any single game module.
/// Kept in Domain so both <c>Application</c> handlers and <c>Domain</c>
/// aggregates can consume them without an extra abstraction layer.
/// </summary>
public static class PlatformConstants
{
    /// <summary>
    /// Per-side starting clock budget. Sprint 2 decision: flat 60 s across
    /// every MVP game (Tic-Tac-Toe 3×3 / 6×6 / 9×9, Connect 4). Promote to
    /// a per-<see cref="IGameModule"/> value if a later game needs a
    /// different budget.
    /// </summary>
    public static readonly TimeSpan DefaultClockBudget = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Grace window before a SignalR disconnect is treated as an abandon
    /// (state.md §2.3 / platform-and-games.md §1 #7). The clock keeps
    /// running through this window — disconnecting does not pause it.
    /// Sprint 2 schedules the entry; Sprint 5 wires the <c>OpponentAbandoned</c>
    /// / <c>ClaimVictory</c> reaction on top.
    /// </summary>
    public static readonly TimeSpan DisconnectGrace = TimeSpan.FromSeconds(30);
}
