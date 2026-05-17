namespace PlayMe.Domain.Games.TicTacToe9x9;

/// <summary>
/// Reject-key vocabulary owned by the Tic-Tac-Toe 9×9 module. These strings
/// are the contract between this module and the TTT 9×9 web renderer
/// (CLAUDE.md §7 "Platform thinness" — the platform never enumerates them).
/// The web side maps each key to a localized message via the shared i18n
/// catalog; the module doesn't care how the rendering happens. Independent
/// of (and intentionally not shared with) the analogous type in the
/// Tic-Tac-Toe 3×3 module — per-module duplication is acceptable.
/// </summary>
public static class TicTacToe9x9Errors
{
    /// <summary>Move payload malformed (e.g. missing or non-numeric `cell`).</summary>
    public const string ValidationMove = "errors.validation.move";

    /// <summary>Cell index out of range (not 0..80).</summary>
    public const string IllegalCell = "errors.move.illegalCell";

    /// <summary>Target cell is already occupied.</summary>
    public const string CellOccupied = "errors.move.cellOccupied";
}
