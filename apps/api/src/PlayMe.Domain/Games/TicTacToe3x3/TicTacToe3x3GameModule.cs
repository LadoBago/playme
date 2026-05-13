using System.Text.Json;
using System.Text.Json.Serialization;
using PlayMe.Domain.Platform;

namespace PlayMe.Domain.Games.TicTacToe3x3;

/// <summary>
/// Tic-Tac-Toe 3×3 rules: players alternate placing X/O; the first to align
/// 3 consecutive marks (row, column, or diagonal) wins. Board fills with no
/// line → draw. X moves first.
///
/// All vocabulary (sides, reject keys, state shape, winning-line shape) is
/// per-module — the platform never inspects any of it (CLAUDE.md §7
/// "Platform thinness").
/// </summary>
public sealed class TicTacToe3x3GameModule : IGameModule
{
    public static readonly GameId GameId = new("tictactoe-3x3");

    private static readonly string[] ValidSidesArray = { TicTacToeSides.X, TicTacToeSides.O };

    /// <summary>
    /// All 8 winning lines on a 3×3 board: 3 rows, 3 cols, 2 diagonals.
    /// Stored as (cellIndex0..8) for direct lookup against the row-major
    /// state array.
    /// </summary>
    private static readonly int[][] WinningLines =
    {
        new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7, 8 }, // rows
        new[] { 0, 3, 6 }, new[] { 1, 4, 7 }, new[] { 2, 5, 8 }, // cols
        new[] { 0, 4, 8 }, new[] { 2, 4, 6 },                    // diagonals
    };

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public GameId Id => GameId;

    public IReadOnlyList<string> ValidSides => ValidSidesArray;

    public string FirstMoveSide => TicTacToeSides.X;

    public TimeSpan DefaultClockBudget { get; } = TimeSpan.FromSeconds(60);

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
            return MoveResult.Reject(TicTacToeErrors.IllegalCell);
        }
        if (!board.IsCellEmpty(cell))
        {
            return MoveResult.Reject(TicTacToeErrors.CellOccupied);
        }

        // Probe for a winning line before constructing the new state so
        // the line travels with the state's WithMove call.
        var winningLine = FindWinningLineFor(board, cell, side);
        var newState = board.WithMove(cell, side, winningLine);

        if (winningLine is not null)
        {
            return MoveResult.Accept(newState, new Win(side));
        }

        if (newState.IsFull())
        {
            return MoveResult.Accept(newState, new Draw());
        }

        return MoveResult.Accept(newState);
    }

    public string Serialize(IGameState state)
    {
        if (state is not TicTacToe3x3State board)
        {
            throw new ArgumentException(
                $"Expected {nameof(TicTacToe3x3State)}, got {state.GetType().Name}.", nameof(state));
        }
        var payload = new StatePayload(
            TicTacToe3x3State.Size,
            TicTacToe3x3State.Size,
            board.Cells,
            board.LastMove,
            board.WinningLine);
        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    public IGameState Deserialize(string serialized)
    {
        ArgumentNullException.ThrowIfNull(serialized);
        StatePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<StatePayload>(serialized, SerializerOptions);
        }
        catch (JsonException e)
        {
            throw new ArgumentException("Failed to parse TicTacToe3x3 state.", nameof(serialized), e);
        }
        if (payload is null)
        {
            throw new ArgumentException("TicTacToe3x3 state blob was null.", nameof(serialized));
        }
        if (payload.Rows != TicTacToe3x3State.Size ||
            payload.Cols != TicTacToe3x3State.Size ||
            payload.Cells is null ||
            payload.Cells.Count != TicTacToe3x3State.CellCount)
        {
            throw new ArgumentException(
                $"TicTacToe3x3 state shape mismatch: rows={payload.Rows}, cols={payload.Cols}, cells={payload.Cells?.Count ?? 0}.",
                nameof(serialized));
        }
        return new TicTacToe3x3State(payload.Cells, payload.LastMove, payload.WinningLine);
    }

    /// <summary>
    /// Return the line through <paramref name="cell"/> on which
    /// <paramref name="side"/> has just completed three in a row, or null
    /// if no win. We only check the lines containing the latest cell —
    /// every earlier winning configuration would have ended the match.
    /// </summary>
    private static BoardCoordinate[]? FindWinningLineFor(
        TicTacToe3x3State board, int cell, string side)
    {
        foreach (var line in WinningLines)
        {
            if (line[0] != cell && line[1] != cell && line[2] != cell) continue;

            var a = line[0] == cell ? side : board.CellAt(line[0]);
            var b = line[1] == cell ? side : board.CellAt(line[1]);
            var c = line[2] == cell ? side : board.CellAt(line[2]);
            if (a == side && b == side && c == side)
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

    /// <summary>Wire shape of TTT-3×3's serialized state. Per-game and opaque
    /// to the platform.</summary>
    private sealed record StatePayload(
        int Rows,
        int Cols,
        IReadOnlyList<string?> Cells,
        int? LastMove = null,
        IReadOnlyList<BoardCoordinate>? WinningLine = null);
}
