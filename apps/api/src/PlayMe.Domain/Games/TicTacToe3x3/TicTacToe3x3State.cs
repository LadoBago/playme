using PlayMe.Domain.Platform;

namespace PlayMe.Domain.Games.TicTacToe3x3;

/// <summary>
/// Immutable snapshot of a 3×3 Tic-Tac-Toe board. Cells are row-major and
/// hold either a side identifier (<see cref="TicTacToeSides.X"/> /
/// <see cref="TicTacToeSides.O"/>) or null for empty.
/// </summary>
public sealed class TicTacToe3x3State : IGameState
{
    public const int Size = 3;
    public const int CellCount = Size * Size;

    private readonly string?[] _cells;

    public TicTacToe3x3State()
    {
        _cells = new string?[CellCount];
    }

    public TicTacToe3x3State(IReadOnlyList<string?> cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        if (cells.Count != CellCount)
        {
            throw new ArgumentException($"Expected {CellCount} cells.", nameof(cells));
        }

        _cells = new string?[CellCount];
        for (var i = 0; i < CellCount; i++) _cells[i] = cells[i];
    }

    public IReadOnlyList<string?> Cells => _cells;

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
    /// was empty.
    /// </summary>
    public TicTacToe3x3State WithCell(int index, string side)
    {
        var copy = (string?[])_cells.Clone();
        copy[index] = side;
        return new TicTacToe3x3State(copy);
    }
}
