using PlayMe.Domain.Platform;

namespace PlayMe.Domain.Games.TicTacToe3x3;

/// <summary>
/// Tic-Tac-Toe 3×3 rules (CLAUDE.md §2.3 canonical spec): players alternate
/// placing X/O; the first to align 3 consecutive marks (row, column, or
/// diagonal) wins. Board fills with no line → draw. X moves first.
/// </summary>
public sealed class TicTacToe3x3GameModule : IGameModule
{
    public static readonly GameId GameId = new("tictactoe-3x3");

    private static readonly string[] _validSides = { TicTacToeSides.X, TicTacToeSides.O };

    /// <summary>
    /// All 8 winning lines on a 3×3 board: 3 rows, 3 cols, 2 diagonals.
    /// Stored as (cellIndex0..8) for direct lookup against the row-major
    /// state array.
    /// </summary>
    private static readonly int[][] _winningLines =
    {
        new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7, 8 }, // rows
        new[] { 0, 3, 6 }, new[] { 1, 4, 7 }, new[] { 2, 5, 8 }, // cols
        new[] { 0, 4, 8 }, new[] { 2, 4, 6 },                    // diagonals
    };

    public GameId Id => GameId;

    public IReadOnlyList<string> ValidSides => _validSides;

    public string FirstMoveSide => TicTacToeSides.X;

    public string OtherSide(string side) => side switch
    {
        TicTacToeSides.X => TicTacToeSides.O,
        TicTacToeSides.O => TicTacToeSides.X,
        _ => throw new ArgumentException($"Unknown side '{side}'.", nameof(side)),
    };

    public IGameState NewMatch() => new TicTacToe3x3State();

    public MoveResult ApplyMove(IGameState state, string side, GameMove move)
    {
        if (state is not TicTacToe3x3State board)
        {
            throw new ArgumentException(
                $"Expected {nameof(TicTacToe3x3State)}, got {state.GetType().Name}.", nameof(state));
        }
        if (move is not TicTacToeMove tttMove)
        {
            throw new ArgumentException(
                $"Expected {nameof(TicTacToeMove)}, got {move.GetType().Name}.", nameof(move));
        }
        if (side != TicTacToeSides.X && side != TicTacToeSides.O)
        {
            throw new ArgumentException($"Unknown side '{side}'.", nameof(side));
        }

        var cell = tttMove.Cell;
        if (cell < 0 || cell >= TicTacToe3x3State.CellCount)
        {
            return MoveResult.Reject(MoveRejectReason.IllegalCell);
        }
        if (!board.IsCellEmpty(cell))
        {
            return MoveResult.Reject(MoveRejectReason.CellOccupied);
        }

        var newState = board.WithCell(cell, side);

        var winningLine = FindWinningLine(newState, side);
        if (winningLine is not null)
        {
            return MoveResult.Accept(newState, new Win(side, winningLine));
        }

        if (newState.IsFull())
        {
            return MoveResult.Accept(newState, new Draw());
        }

        return MoveResult.Accept(newState);
    }

    /// <summary>
    /// Return the first winning line on which <paramref name="side"/> has
    /// all three cells, or null if no win. Coordinates are returned in
    /// (row, col) form so the client can highlight them directly.
    /// </summary>
    private static BoardCoordinate[]? FindWinningLine(
        TicTacToe3x3State board, string side)
    {
        foreach (var line in _winningLines)
        {
            if (board.CellAt(line[0]) == side &&
                board.CellAt(line[1]) == side &&
                board.CellAt(line[2]) == side)
            {
                return new[]
                {
                    ToCoordinate(line[0]),
                    ToCoordinate(line[1]),
                    ToCoordinate(line[2]),
                };
            }
        }
        return null;
    }

    private static BoardCoordinate ToCoordinate(int cellIndex) =>
        new(cellIndex / TicTacToe3x3State.Size, cellIndex % TicTacToe3x3State.Size);
}
