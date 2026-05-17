using System.Text.Json;
using System.Text.Json.Serialization;
using PlayMe.Domain.Platform;

namespace PlayMe.Domain.Games.TicTacToe9x9;

/// <summary>
/// Tic-Tac-Toe 9×9 rules (Gomoku-style on the 9×9 board): players alternate
/// placing X / O on a 9×9 grid. First to align **at least 5 consecutive**
/// marks horizontally, vertically, or on either diagonal wins; a run of 6,
/// 7, 8, or 9 in a row also counts as a single win and the whole run is
/// reported as the winning line. Board fills with no line → draw. X moves
/// first. No wraparound. No swap / pro / balancing rule in v1 — first-player
/// advantage on 9×9 is accepted for casual play (see
/// <see href="../../../../../docs/platform-and-games.md">platform-and-games.md §2.2</see>).
///
/// All vocabulary (sides, reject keys, state shape, winning-line shape) is
/// per-module — the platform never inspects any of it (CLAUDE.md §7
/// "Platform thinness"). Win detection sweeps the four line directions from
/// the just-played cell rather than enumerating every 5-line on the board
/// (192 of them for exactly-5; many more once 6/7/8/9 are valid runs too).
/// </summary>
public sealed class TicTacToe9x9GameModule : IGameModule
{
    public static readonly GameId GameId = new("tictactoe-9x9");

    private const int MinRunLength = 5;

    private static readonly string[] ValidSidesArray =
        { TicTacToe9x9Sides.X, TicTacToe9x9Sides.O };

    /// <summary>
    /// Four direction vectors (delta-row, delta-col) covering every line a
    /// just-played mark can complete: horizontal (→), vertical (↓), and the
    /// two diagonals (↘, ↗). The win probe sweeps each direction both ways
    /// from the just-played cell.
    /// </summary>
    private static readonly (int DRow, int DCol)[] Directions =
    {
        (0, 1),   // horizontal
        (1, 0),   // vertical
        (1, 1),   // diagonal ↘
        (-1, 1),  // diagonal ↗
    };

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public GameId Id => GameId;

    public IReadOnlyList<string> ValidSides => ValidSidesArray;

    public string FirstMoveSide => TicTacToe9x9Sides.X;

    public TimeSpan DefaultClockBudget { get; } = TimeSpan.FromMinutes(10);

    public string OtherSide(string side) => side switch
    {
        TicTacToe9x9Sides.X => TicTacToe9x9Sides.O,
        TicTacToe9x9Sides.O => TicTacToe9x9Sides.X,
        _ => throw new ArgumentException($"Unknown side '{side}'.", nameof(side)),
    };

    public IGameState NewMatch() => new TicTacToe9x9State();

    public MoveResult ApplyMove(IGameState state, string side, GameMove move)
    {
        if (state is not TicTacToe9x9State board)
        {
            throw new ArgumentException(
                $"Expected {nameof(TicTacToe9x9State)}, got {state.GetType().Name}.", nameof(state));
        }
        if (move is not TicTacToe9x9Move tttMove)
        {
            throw new ArgumentException(
                $"Expected {nameof(TicTacToe9x9Move)}, got {move.GetType().Name}.", nameof(move));
        }
        if (side != TicTacToe9x9Sides.X && side != TicTacToe9x9Sides.O)
        {
            throw new ArgumentException($"Unknown side '{side}'.", nameof(side));
        }

        var cell = tttMove.Cell;
        if (cell < 0 || cell >= TicTacToe9x9State.CellCount)
        {
            return MoveResult.Reject(TicTacToe9x9Errors.IllegalCell);
        }
        if (!board.IsCellEmpty(cell))
        {
            return MoveResult.Reject(TicTacToe9x9Errors.CellOccupied);
        }

        // Probe for a winning line before constructing the new state so the
        // line travels with the state's WithMove call.
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
        if (state is not TicTacToe9x9State board)
        {
            throw new ArgumentException(
                $"Expected {nameof(TicTacToe9x9State)}, got {state.GetType().Name}.", nameof(state));
        }
        var payload = new StatePayload(
            TicTacToe9x9State.Size,
            TicTacToe9x9State.Size,
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
            throw new ArgumentException("Failed to parse TicTacToe9x9 state.", nameof(serialized), e);
        }
        if (payload is null)
        {
            throw new ArgumentException("TicTacToe9x9 state blob was null.", nameof(serialized));
        }
        if (payload.Rows != TicTacToe9x9State.Size ||
            payload.Cols != TicTacToe9x9State.Size ||
            payload.Cells is null ||
            payload.Cells.Count != TicTacToe9x9State.CellCount)
        {
            throw new ArgumentException(
                $"TicTacToe9x9 state shape mismatch: rows={payload.Rows}, cols={payload.Cols}, cells={payload.Cells?.Count ?? 0}.",
                nameof(serialized));
        }
        return new TicTacToe9x9State(payload.Cells, payload.LastMove, payload.WinningLine);
    }

    /// <summary>
    /// Return the run through <paramref name="cell"/> that <paramref name="side"/>
    /// has just completed (length ≥ 5), or null if no win. We only inspect
    /// lines containing the latest cell — every earlier winning configuration
    /// would already have ended the match. For each of the four directions
    /// we walk backwards to the run's anchor cell, then forwards counting
    /// consecutive same-side cells; if the run is ≥ 5 we collect the whole
    /// run as the winning line (so a 6/7/8/9-in-a-row reports its full extent
    /// for the renderer to highlight).
    /// </summary>
    private static TicTacToe9x9Coordinate[]? FindWinningLineFor(
        TicTacToe9x9State board, int cell, string side)
    {
        var row = cell / TicTacToe9x9State.Size;
        var col = cell % TicTacToe9x9State.Size;

        foreach (var (dr, dc) in Directions)
        {
            var startRow = row;
            var startCol = col;
            // Walk backwards along (-dr, -dc) to the run's anchor cell. The
            // just-played cell is virtually filled with `side` from the
            // caller's perspective; any other cell we read from the board.
            while (true)
            {
                var prevRow = startRow - dr;
                var prevCol = startCol - dc;
                if (prevRow < 0 || prevRow >= TicTacToe9x9State.Size ||
                    prevCol < 0 || prevCol >= TicTacToe9x9State.Size)
                    break;
                var prevSide = (prevRow == row && prevCol == col)
                    ? side
                    : board.CellAt(prevRow * TicTacToe9x9State.Size + prevCol);
                if (prevSide != side) break;
                startRow = prevRow;
                startCol = prevCol;
            }

            // Walk forwards counting consecutive same-side cells.
            var runLength = 0;
            var r = startRow;
            var c = startCol;
            while (r >= 0 && r < TicTacToe9x9State.Size && c >= 0 && c < TicTacToe9x9State.Size)
            {
                var here = (r == row && c == col)
                    ? side
                    : board.CellAt(r * TicTacToe9x9State.Size + c);
                if (here != side) break;
                runLength++;
                r += dr;
                c += dc;
            }

            if (runLength >= MinRunLength)
            {
                var line = new TicTacToe9x9Coordinate[runLength];
                for (var i = 0; i < runLength; i++)
                {
                    line[i] = new TicTacToe9x9Coordinate(startRow + dr * i, startCol + dc * i);
                }
                return line;
            }
        }
        return null;
    }

    /// <summary>Wire shape of TTT-9×9's serialized state. Per-game and
    /// opaque to the platform.</summary>
    private sealed record StatePayload(
        int Rows,
        int Cols,
        IReadOnlyList<string?> Cells,
        int? LastMove = null,
        IReadOnlyList<TicTacToe9x9Coordinate>? WinningLine = null);
}
