namespace PlayMe.Domain.Games.TicTacToe6x6;

/// <summary>
/// Reject-key vocabulary owned by the Tic-Tac-Toe 6×6 module. These strings
/// are the contract between this module and the TTT-6×6 web renderer
/// (CLAUDE.md §7 "Platform thinness" — the platform never enumerates them).
/// The web side maps each key to a localized message via the shared i18n
/// catalog. Reuses the cross-TTT i18n keys (`errors.move.illegalCell`,
/// `errors.move.cellOccupied`, `errors.validation.move`) for the localized
/// strings because the user-facing error vocabulary is identical for every
/// Tic-Tac-Toe variant; the underlying constants stay per-module so each
/// module is independently complete.
/// </summary>
public static class TicTacToe6x6Errors
{
    /// <summary>Move payload malformed (e.g. missing or non-numeric `cell`).</summary>
    public const string ValidationMove = "errors.validation.move";

    /// <summary>Cell index out of range (not 0..35).</summary>
    public const string IllegalCell = "errors.move.illegalCell";

    /// <summary>Target cell is already occupied.</summary>
    public const string CellOccupied = "errors.move.cellOccupied";
}
