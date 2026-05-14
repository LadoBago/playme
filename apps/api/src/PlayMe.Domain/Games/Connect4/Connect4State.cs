using PlayMe.Domain.Platform;

namespace PlayMe.Domain.Games.Connect4;

/// <summary>
/// Immutable snapshot of a Connect 4 board: 7 columns × 6 rows. Cells are
/// stored row-major with row 0 at the top of the board and row 5 at the
/// bottom, so "gravity" lands a new disc at the highest row index whose
/// cell is null in the chosen column.
///
/// Carries the rendering-hint fields the Connect 4 web renderer consumes
/// — <see cref="LastMove"/> (highlight the just-played disc) and
/// <see cref="WinningLine"/> (highlight the four discs aligned by the
/// winning move). The platform never inspects these; they are part of the
/// module's serialized shape (CLAUDE.md §7 "Platform thinness").
/// </summary>
public sealed class Connect4State : IGameState
{
    public const int Cols = 7;
    public const int Rows = 6;
    public const int CellCount = Cols * Rows;

    private readonly string?[] _cells;

    public IReadOnlyList<string?> Cells => _cells;

    /// <summary>The just-dropped disc's coordinate, or null on an empty board.</summary>
    public Connect4Coordinate? LastMove { get; }

    /// <summary>Four cells aligned by the winning move, or null if the
    /// match has not been won.</summary>
    public IReadOnlyList<Connect4Coordinate>? WinningLine { get; }

    public Connect4State()
    {
        _cells = new string?[CellCount];
        LastMove = null;
        WinningLine = null;
    }

    public Connect4State(
        IReadOnlyList<string?> cells,
        Connect4Coordinate? lastMove = null,
        IReadOnlyList<Connect4Coordinate>? winningLine = null)
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

    public static int IndexOf(int row, int col) => row * Cols + col;

    public string? CellAt(int row, int col) => _cells[IndexOf(row, col)];

    /// <summary>
    /// The row a disc dropped into <paramref name="col"/> would occupy, or
    /// null if the column is full. The lowest empty row (largest row index)
    /// wins by gravity.
    /// </summary>
    public int? LandingRowFor(int col)
    {
        if (col < 0 || col >= Cols) return null;
        for (var row = Rows - 1; row >= 0; row--)
        {
            if (_cells[IndexOf(row, col)] is null) return row;
        }
        return null;
    }

    public bool IsFull()
    {
        // A board is full iff every column's top row is occupied. Cheaper
        // than scanning all 42 cells.
        for (var col = 0; col < Cols; col++)
        {
            if (_cells[IndexOf(0, col)] is null) return false;
        }
        return true;
    }

    /// <summary>
    /// Return a new state with a disc of <paramref name="side"/> dropped at
    /// (<paramref name="row"/>, <paramref name="col"/>). Caller must have
    /// already verified the landing row via <see cref="LandingRowFor"/>.
    /// <paramref name="winningLine"/> is non-null only when the move ended
    /// the match with a win.
    /// </summary>
    public Connect4State WithDrop(
        int row, int col, string side, IReadOnlyList<Connect4Coordinate>? winningLine = null)
    {
        var copy = (string?[])_cells.Clone();
        copy[IndexOf(row, col)] = side;
        return new Connect4State(copy, new Connect4Coordinate(row, col), winningLine);
    }
}
