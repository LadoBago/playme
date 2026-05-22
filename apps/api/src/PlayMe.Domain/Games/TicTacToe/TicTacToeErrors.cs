namespace PlayMe.Domain.Games.TicTacToe;

/// <summary>
/// Reject-key vocabulary owned by the unified Tic-Tac-Toe module
/// (Sprint 9 PR1b). These strings are the contract between this module
/// and the unified-TTT web renderer (CLAUDE.md §7 "Platform thinness").
/// Same i18n keys the legacy per-size modules use — they remain
/// localized in the shared catalog (<c>errors.move.illegalCell</c> etc.).
/// </summary>
public static class TicTacToeErrors
{
    /// <summary>Move payload malformed (e.g. missing or non-numeric `cell`).</summary>
    public const string ValidationMove = "errors.validation.move";

    /// <summary>Cell index out of range for the host-chosen board size.</summary>
    public const string IllegalCell = "errors.move.illegalCell";

    /// <summary>Target cell is already occupied.</summary>
    public const string CellOccupied = "errors.move.cellOccupied";

    /// <summary>
    /// Per-room options blob is missing, malformed, or carries a
    /// <c>boardSize</c> outside the allowed set. The same platform-level
    /// i18n key the seam handler reports (Sprint 9 PR1a).
    /// </summary>
    public const string ConfigInvalidGameOptions = "errors.config.invalidGameOptions";
}
