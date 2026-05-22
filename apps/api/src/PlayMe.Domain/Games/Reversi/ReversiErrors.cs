namespace PlayMe.Domain.Games.Reversi;

/// <summary>
/// Reject-key vocabulary owned by the Reversi module. These strings are the
/// contract between this module and the Reversi web renderer (CLAUDE.md §7
/// "Platform thinness" — the platform never enumerates them). The web side
/// maps each key to a localized message via the shared i18n catalog.
/// </summary>
public static class ReversiErrors
{
    /// <summary>Move payload malformed (e.g. missing coordinates and not a pass).</summary>
    public const string ValidationMove = "errors.validation.move";

    /// <summary>Placement (row, col) outside the 0..7 × 0..7 board.</summary>
    public const string OutOfBounds = "errors.move.outOfBounds";

    /// <summary>Placement target is already occupied.</summary>
    public const string CellOccupied = "errors.move.cellOccupied";

    /// <summary>
    /// Opening-phase placement (moves 1–4) outside the central 2×2 squares
    /// (rows 3–4 × cols 3–4).
    /// </summary>
    public const string OpeningMustBeCentral = "errors.move.openingMustBeCentral";

    /// <summary>
    /// Standard-play placement that brackets no opponent disc — illegal.
    /// </summary>
    public const string MustBracket = "errors.move.mustBracket";

    /// <summary>
    /// Pass submitted while the player has at least one legal placement.
    /// Defends against a buggy or malicious client.
    /// </summary>
    public const string PassNotAllowed = "errors.move.passNotAllowed";
}
