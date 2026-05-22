using PlayMe.Domain.Platform;

namespace PlayMe.Domain.Games.TicTacToe;

/// <summary>
/// Immutable snapshot of a Tic-Tac-Toe board on an N×N grid, where
/// <see cref="BoardSize"/> is the host-chosen <c>boardSize</c> option
/// (Sprint 9 PR1b — unified module). Cells are row-major and hold either
/// a side identifier (<see cref="TicTacToeSides.X"/> / <see cref="TicTacToeSides.O"/>)
/// or null for empty.
/// <para>
/// <see cref="WinLength"/> is the consecutive-mark count that ends the
/// match (derived deterministically from <see cref="BoardSize"/> by the
/// module; the state stores it so the serialized blob is self-describing
/// and round-trips correctly without re-deriving on rehydrate).
/// </para>
/// <para>
/// Carries per-game "rendering hint" fields the TTT web renderer consumes
/// — <see cref="LastMove"/> for the move-highlight, <see cref="WinningLine"/>
/// for the win-line highlight. The platform never inspects these.
/// </para>
/// </summary>
public sealed class TicTacToeState : IGameState
{
    public int BoardSize { get; }
    public int WinLength { get; }
    public int CellCount => BoardSize * BoardSize;

    private readonly string?[] _cells;

    public IReadOnlyList<string?> Cells => _cells;

    /// <summary>Cell index 0..<see cref="CellCount"/>-1 of the most-
    /// recently-played move, or null for an empty board.</summary>
    public int? LastMove { get; }

    /// <summary>Cells aligned by the winning move (length ≥ <see cref="WinLength"/>),
    /// or null if the match has not been won. A run longer than the minimum
    /// reports its full extent so the renderer highlights every cell in the
    /// run (matters on 6×6 and 9×9, where 5-, 6-, 7-, 8-, or 9-in-a-row are
    /// all valid wins).</summary>
    public IReadOnlyList<TicTacToeCoordinate>? WinningLine { get; }

    public TicTacToeState(int boardSize, int winLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(boardSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(winLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(winLength, boardSize);
        BoardSize = boardSize;
        WinLength = winLength;
        _cells = new string?[boardSize * boardSize];
        LastMove = null;
        WinningLine = null;
    }

    public TicTacToeState(
        int boardSize,
        int winLength,
        IReadOnlyList<string?> cells,
        int? lastMove = null,
        IReadOnlyList<TicTacToeCoordinate>? winningLine = null)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(boardSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(winLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(winLength, boardSize);
        if (cells.Count != boardSize * boardSize)
        {
            throw new ArgumentException(
                $"Expected {boardSize * boardSize} cells.", nameof(cells));
        }

        BoardSize = boardSize;
        WinLength = winLength;
        _cells = new string?[cells.Count];
        for (var i = 0; i < cells.Count; i++) _cells[i] = cells[i];
        LastMove = lastMove;
        WinningLine = winningLine;
    }

    public string? CellAt(int index) => _cells[index];

    public bool IsCellEmpty(int index) => _cells[index] is null;

    public bool IsFull()
    {
        for (var i = 0; i < _cells.Length; i++)
        {
            if (_cells[i] is null) return false;
        }
        return true;
    }

    /// <summary>
    /// Return a new state with <paramref name="index"/> set to
    /// <paramref name="side"/>. Caller must have verified the cell was empty.
    /// <paramref name="winningLine"/> is non-null only when the move ended
    /// the match with a win.
    /// </summary>
    public TicTacToeState WithMove(
        int index,
        string side,
        IReadOnlyList<TicTacToeCoordinate>? winningLine = null)
    {
        var copy = (string?[])_cells.Clone();
        copy[index] = side;
        return new TicTacToeState(BoardSize, WinLength, copy, index, winningLine);
    }
}
