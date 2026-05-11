namespace PlayMe.Domain.Platform;

/// <summary>
/// Why a <see cref="IGameModule.ApplyMove"/> call rejected a move. Maps 1:1
/// to the <c>errors.move.*</c> i18n keys (CLAUDE.md §3) so the API layer can
/// translate without per-game logic.
/// </summary>
public enum MoveRejectReason
{
    /// <summary>Cell index out of range, column out of range, or otherwise
    /// malformed for this game.</summary>
    IllegalCell,

    /// <summary>Connect 4: target column is already full.</summary>
    FullColumn,

    /// <summary>Tic-Tac-Toe: target cell is already occupied.</summary>
    CellOccupied,
}
