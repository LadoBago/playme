using PlayMe.Domain.Platform;

namespace PlayMe.Domain.Games.TicTacToe6x6;

/// <summary>
/// Immutable snapshot of a 6×6 Tic-Tac-Toe board. Cells are row-major and
/// hold either a side identifier (<see cref="TicTacToe6x6Sides.X"/> /
/// <see cref="TicTacToe6x6Sides.O"/>) or null for empty.
///
/// Carries the per-game "rendering hint" fields the TTT-6×6 web renderer
/// consumes — <see cref="LastMove"/> for the move-highlight, <see cref="WinningLine"/>
/// for the win-line highlight. The platform never inspects these; they are
/// part of the module's serialized shape (CLAUDE.md §7 "Platform thinness").
/// </summary>
public sealed class TicTacToe6x6State : IGameState
{
    public const int Size = 6;
    public const int CellCount = Size * Size;

    private readonly string?[] _cells;

    public IReadOnlyList<string?> Cells => _cells;

    /// <summary>Cell index 0..35 of the most-recently-played move, or null
    /// for an empty board.</summary>
    public int? LastMove { get; }

    /// <summary>Cells aligned by the winning move (4, 5, or 6 in a row), or
    /// null if the match has not been won. A run longer than 4 is reported
    /// in full — the 6×6 rule (`platform-and-games.md §2.1`) is "at least 4
    /// consecutive," so 5- and 6-cell winning lines are valid wins, not
    /// separately scored.</summary>
    public IReadOnlyList<TicTacToe6x6Coordinate>? WinningLine { get; }

    public TicTacToe6x6State()
    {
        _cells = new string?[CellCount];
        LastMove = null;
        WinningLine = null;
    }

    public TicTacToe6x6State(
        IReadOnlyList<string?> cells,
        int? lastMove = null,
        IReadOnlyList<TicTacToe6x6Coordinate>? winningLine = null)
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
    public TicTacToe6x6State WithMove(
        int index, string side, IReadOnlyList<TicTacToe6x6Coordinate>? winningLine = null)
    {
        var copy = (string?[])_cells.Clone();
        copy[index] = side;
        return new TicTacToe6x6State(copy, index, winningLine);
    }
}
