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
    /// Pause the platform inserts between a bot's turn becoming due and
    /// its move being applied (Sprint 12 seam D; docs/bots.md). Purely
    /// presentational — an instant reply on a 3×3 board reads as a glitch.
    /// The module's own compute time comes on top; the bot's chess clock
    /// runs through both.
    /// </summary>
    public static readonly TimeSpan BotThinkDelay = TimeSpan.FromMilliseconds(700);

    /// <summary>
    /// Stored display name for the bot seat. Not user-facing: the web
    /// renders a bot from <see cref="PlayerKind.Bot"/> through an i18n key
    /// and never shows this string. It exists only because every
    /// <see cref="Player"/> carries a <see cref="DisplayName"/>.
    /// </summary>
    public const string BotDisplayName = "Bot";

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
