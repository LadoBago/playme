using System.Text.Json;
using System.Text.Json.Serialization;
using PlayMe.Domain.Platform;

namespace PlayMe.Domain.Games.TicTacToe6x6;

/// <summary>
/// Tic-Tac-Toe 6×6 rules: players alternate placing X/O on a 6×6 grid; the
/// first to align **at least 4 consecutive** marks (row, column, or either
/// diagonal) wins. A run of 5 or 6 counts as a win, not separately. Board
/// fills with no line → draw. X moves first. No wraparound, no swap/pro
/// rule (`platform-and-games.md §2.1`).
///
/// All vocabulary (sides, reject keys, state shape, winning-line shape) is
/// per-module — the platform never inspects any of it (CLAUDE.md §7
/// "Platform thinness"). Intentionally **not** sharing code with the 3×3
/// or 9×9 modules: per-game duplication is the contract (CLAUDE.md §7).
/// </summary>
public sealed class TicTacToe6x6GameModule : IGameModule
{
    public static readonly GameId GameId = new("tictactoe-6x6");

    /// <summary>
    /// Minimum number of consecutive same-side marks that constitute a win
    /// on this board (`platform-and-games.md §2.1`). Runs of 5 or 6 also
    /// win — the detector reports the full run.
    /// </summary>
    private const int MinRunLength = 4;

    private static readonly string[] ValidSidesArray = { TicTacToe6x6Sides.X, TicTacToe6x6Sides.O };

    /// <summary>
    /// Four direction vectors (delta-row, delta-col) covering every line a
    /// just-played cell can complete: horizontal (→), vertical (↓), and the
    /// two diagonals (↘, ↗). The win probe sweeps each direction both ways
    /// from the just-played cell — the same dispatch shape the Connect 4
    /// module uses, intentionally duplicated rather than extracted
    /// (CLAUDE.md §7 "Platform thinness").
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

    public string FirstMoveSide => TicTacToe6x6Sides.X;

    /// <summary>
    /// Default clock budget for 6×6 is 3 minutes per player
    /// (`platform-and-games.md §1 #3` "per-game defaults"). All three
    /// presets (1/3/10 min) remain selectable; this is just the
    /// preselected option on the configure page.
    /// </summary>
    public TimeSpan DefaultClockBudget { get; } = TimeSpan.FromMinutes(3);

    public string OtherSide(string side) => side switch
    {
        TicTacToe6x6Sides.X => TicTacToe6x6Sides.O,
        TicTacToe6x6Sides.O => TicTacToe6x6Sides.X,
        _ => throw new ArgumentException($"Unknown side '{side}'.", nameof(side)),
    };

    public string? ValidateOptions(JsonElement? options) =>
        options is null ? null : "errors.config.invalidGameOptions";

    public IGameState NewMatch(JsonElement? options) => new TicTacToe6x6State();

    public MoveResult ApplyMove(IGameState state, string side, GameMove move)
    {
        if (state is not TicTacToe6x6State board)
        {
            throw new ArgumentException(
                $"Expected {nameof(TicTacToe6x6State)}, got {state.GetType().Name}.", nameof(state));
        }
        if (move is not TicTacToe6x6Move tttMove)
        {
            throw new ArgumentException(
                $"Expected {nameof(TicTacToe6x6Move)}, got {move.GetType().Name}.", nameof(move));
        }
        if (side != TicTacToe6x6Sides.X && side != TicTacToe6x6Sides.O)
        {
            throw new ArgumentException($"Unknown side '{side}'.", nameof(side));
        }

        var cell = tttMove.Cell;
        if (cell < 0 || cell >= TicTacToe6x6State.CellCount)
        {
            return MoveResult.Reject(TicTacToe6x6Errors.IllegalCell);
        }
        if (!board.IsCellEmpty(cell))
        {
            return MoveResult.Reject(TicTacToe6x6Errors.CellOccupied);
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
        if (state is not TicTacToe6x6State board)
        {
            throw new ArgumentException(
                $"Expected {nameof(TicTacToe6x6State)}, got {state.GetType().Name}.", nameof(state));
        }
        var payload = new StatePayload(
            TicTacToe6x6State.Size,
            TicTacToe6x6State.Size,
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
            throw new ArgumentException("Failed to parse TicTacToe6x6 state.", nameof(serialized), e);
        }
        if (payload is null)
        {
            throw new ArgumentException("TicTacToe6x6 state blob was null.", nameof(serialized));
        }
        if (payload.Rows != TicTacToe6x6State.Size ||
            payload.Cols != TicTacToe6x6State.Size ||
            payload.Cells is null ||
            payload.Cells.Count != TicTacToe6x6State.CellCount)
        {
            throw new ArgumentException(
                $"TicTacToe6x6 state shape mismatch: rows={payload.Rows}, cols={payload.Cols}, cells={payload.Cells?.Count ?? 0}.",
                nameof(serialized));
        }
        return new TicTacToe6x6State(payload.Cells, payload.LastMove, payload.WinningLine);
    }

    /// <summary>
    /// Return the line of <see cref="MinRunLength"/>-or-more cells through
    /// (<paramref name="cell"/>) that <paramref name="side"/> has just
    /// completed, or null if no win. We only check lines containing the
    /// just-played cell — any earlier winning configuration would already
    /// have ended the match. For each of the four directions we walk
    /// backwards to the run's anchor, then forwards counting consecutive
    /// same-side cells. If the run is ≥ <see cref="MinRunLength"/> we
    /// return its full coordinate sequence (so a 5- or 6-in-a-row reports
    /// every winning cell, not just the first four).
    /// </summary>
    private static TicTacToe6x6Coordinate[]? FindWinningLineFor(
        TicTacToe6x6State board, int cell, string side)
    {
        var row = cell / TicTacToe6x6State.Size;
        var col = cell % TicTacToe6x6State.Size;

        foreach (var (dr, dc) in Directions)
        {
            // Walk backwards along (-dr, -dc) to the run's anchor cell.
            var startRow = row;
            var startCol = col;
            while (true)
            {
                var prevRow = startRow - dr;
                var prevCol = startCol - dc;
                if (prevRow < 0 || prevRow >= TicTacToe6x6State.Size ||
                    prevCol < 0 || prevCol >= TicTacToe6x6State.Size)
                    break;
                // The just-played cell is virtually filled with `side` from
                // the caller's perspective; any other cell reads from the
                // board's current state.
                var prevSide = (prevRow == row && prevCol == col)
                    ? side
                    : board.CellAt(prevRow * TicTacToe6x6State.Size + prevCol);
                if (prevSide != side) break;
                startRow = prevRow;
                startCol = prevCol;
            }

            // Walk forwards counting consecutive same-side cells.
            var runLength = 0;
            var r = startRow;
            var c = startCol;
            while (r >= 0 && r < TicTacToe6x6State.Size && c >= 0 && c < TicTacToe6x6State.Size)
            {
                var here = (r == row && c == col)
                    ? side
                    : board.CellAt(r * TicTacToe6x6State.Size + c);
                if (here != side) break;
                runLength++;
                r += dr;
                c += dc;
            }

            if (runLength >= MinRunLength)
            {
                var line = new TicTacToe6x6Coordinate[runLength];
                for (var i = 0; i < runLength; i++)
                {
                    line[i] = new TicTacToe6x6Coordinate(startRow + dr * i, startCol + dc * i);
                }
                return line;
            }
        }
        return null;
    }

    /// <summary>Wire shape of TTT-6×6's serialized state. Per-game and
    /// opaque to the platform.</summary>
    private sealed record StatePayload(
        int Rows,
        int Cols,
        IReadOnlyList<string?> Cells,
        int? LastMove = null,
        IReadOnlyList<TicTacToe6x6Coordinate>? WinningLine = null);
}
