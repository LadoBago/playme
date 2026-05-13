using PlayMe.Domain.Platform;

namespace PlayMe.Domain.Games.TicTacToe3x3;

/// <summary>
/// Immutable snapshot of a 3×3 Tic-Tac-Toe board. Cells are row-major and
/// hold either a side identifier (<see cref="TicTacToeSides.X"/> /
/// <see cref="TicTacToeSides.O"/>) or null for empty.
///
/// Carries the per-game "rendering hint" fields the TTT web renderer
/// consumes — <see cref="LastMove"/> for the move-highlight, <see cref="WinningLine"/>
/// for the win-line highlight. The platform never inspects these; they are
/// part of the module's serialized shape (CLAUDE.md §7 "Platform thinness").
/// </summary>
public sealed class TicTacToe3x3State : IGameState
{
    public const int Size = 3;
    public const int CellCount = Size * Size;

    private readonly string?[] _cells;

    public IReadOnlyList<string?> Cells => _cells;

    /// <summary>Cell index 0..8 of the most-recently-played move, or null
    /// for an empty board.</summary>
    public int? LastMove { get; }

    /// <summary>Three cells aligned by the winning move, or null if the
    /// match has not been won.</summary>
    public IReadOnlyList<BoardCoordinate>? WinningLine { get; }

    public TicTacToe3x3State()
    {
        _cells = new string?[CellCount];
        LastMove = null;
        WinningLine = null;
    }

    public TicTacToe3x3State(
        IReadOnlyList<string?> cells,
        int? lastMove = null,
        IReadOnlyList<BoardCoordinate>? winningLine = null)
    {
        ArgumentNullException.ThrowIfNull(cells);
        if (cells.Count != CellCount)
        {
            throw new ArgumentException($"Expected {CellCount} cells.", nameof(cells));
        }

        _cells = new string?[CellCount];
        for (var i = 0; i < CellCount; i++) _cells[i] = cells[i];
        LastMove = lastMove;
        WinningLine = winningLine;
    }

    public string? CellAt(int index) => _cells[index];

    public bool IsCellEmpty(int index) => _cells[index] is null;

    public bool IsFull()
    {
        for (var i = 0; i < CellCount; i++)
        {
            if (_cells[i] is null) return false;
        }
        return true;
    }

    /// <summary>
    /// Return a new state with <paramref name="index"/> set to
    /// <paramref name="side"/>. Caller must have already verified the cell
    /// was empty. <paramref name="winningLine"/> is non-null only when the
    /// move ended the match with a win.
    /// </summary>
    public TicTacToe3x3State WithMove(int index, string side, IReadOnlyList<BoardCoordinate>? winningLine = null)
    {
        var copy = (string?[])_cells.Clone();
        copy[index] = side;
        return new TicTacToe3x3State(copy, index, winningLine);
    }
}
