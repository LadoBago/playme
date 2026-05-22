using System.Text.Json;
using System.Text.Json.Serialization;
using PlayMe.Domain.Platform;

namespace PlayMe.Domain.Games.Connect4;

/// <summary>
/// Connect 4 rules: players alternate dropping red and yellow discs into a
/// 7-column × 6-row board with gravity (the disc lands in the lowest empty
/// row of the chosen column). First to align four consecutive discs of
/// their colour — horizontally, vertically, or on either diagonal — wins.
/// A full board with no aligned four → draw. Red moves first (Hasbro
/// convention; see <see href="../../../../../docs/platform-and-games.md">platform-and-games.md §2.1</see>).
///
/// All vocabulary (sides, reject keys, state shape, winning-line shape) is
/// per-module — the platform never inspects any of it (CLAUDE.md §7
/// "Platform thinness").
/// </summary>
public sealed class Connect4GameModule : IGameModule
{
    public static readonly GameId GameId = new("connect4");

    private const int RunLength = 4;

    private static readonly string[] ValidSidesArray = { Connect4Sides.Red, Connect4Sides.Yellow };

    /// <summary>
    /// Four direction vectors (delta-row, delta-col) covering every line a
    /// dropped disc can complete: horizontal (→), vertical (↓), and the two
    /// diagonals (↘, ↗). The win probe sweeps each direction both ways from
    /// the landing cell.
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

    public string FirstMoveSide => Connect4Sides.Red;

    public TimeSpan DefaultClockBudget { get; } = TimeSpan.FromMinutes(3);

    public string OtherSide(string side) => side switch
    {
        Connect4Sides.Red => Connect4Sides.Yellow,
        Connect4Sides.Yellow => Connect4Sides.Red,
        _ => throw new ArgumentException($"Unknown side '{side}'.", nameof(side)),
    };

    public string? ValidateOptions(JsonElement? options) =>
        options is null ? null : "errors.config.invalidGameOptions";

    public IGameState NewMatch(JsonElement? options) => new Connect4State();

    public MoveResult ApplyMove(IGameState state, string side, GameMove move)
    {
        if (state is not Connect4State board)
        {
            throw new ArgumentException(
                $"Expected {nameof(Connect4State)}, got {state.GetType().Name}.", nameof(state));
        }
        if (move is not Connect4Move c4Move)
        {
            throw new ArgumentException(
                $"Expected {nameof(Connect4Move)}, got {move.GetType().Name}.", nameof(move));
        }
        if (side != Connect4Sides.Red && side != Connect4Sides.Yellow)
        {
            throw new ArgumentException($"Unknown side '{side}'.", nameof(side));
        }

        var col = c4Move.Column;
        if (col < 0 || col >= Connect4State.Cols)
        {
            return MoveResult.Reject(Connect4Errors.IllegalColumn);
        }

        var landingRow = board.LandingRowFor(col);
        if (landingRow is null)
        {
            return MoveResult.Reject(Connect4Errors.ColumnFull);
        }

        var winningLine = FindWinningLineFor(board, landingRow.Value, col, side);
        var newState = board.WithDrop(landingRow.Value, col, side, winningLine);

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
        if (state is not Connect4State board)
        {
            throw new ArgumentException(
                $"Expected {nameof(Connect4State)}, got {state.GetType().Name}.", nameof(state));
        }
        var payload = new StatePayload(
            Connect4State.Rows,
            Connect4State.Cols,
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
            throw new ArgumentException("Failed to parse Connect 4 state.", nameof(serialized), e);
        }
        if (payload is null)
        {
            throw new ArgumentException("Connect 4 state blob was null.", nameof(serialized));
        }
        if (payload.Rows != Connect4State.Rows ||
            payload.Cols != Connect4State.Cols ||
            payload.Cells is null ||
            payload.Cells.Count != Connect4State.CellCount)
        {
            throw new ArgumentException(
                $"Connect 4 state shape mismatch: rows={payload.Rows}, cols={payload.Cols}, cells={payload.Cells?.Count ?? 0}.",
                nameof(serialized));
        }
        return new Connect4State(payload.Cells, payload.LastMove, payload.WinningLine);
    }

    /// <summary>
    /// Return the line of four through (<paramref name="row"/>, <paramref name="col"/>)
    /// that <paramref name="side"/> has just completed, or null if no win.
    /// We only check lines containing the just-played cell — every earlier
    /// winning configuration would have ended the match. For each of the
    /// four directions we count the run extending both ways from the
    /// landing cell; if the run is ≥ 4 we collect the four anchor coords.
    /// </summary>
    private static Connect4Coordinate[]? FindWinningLineFor(
        Connect4State board, int row, int col, string side)
    {
        foreach (var (dr, dc) in Directions)
        {
            var startRow = row;
            var startCol = col;
            // Walk backwards along (-dr, -dc) to the run's anchor cell.
            while (true)
            {
                var prevRow = startRow - dr;
                var prevCol = startCol - dc;
                if (prevRow < 0 || prevRow >= Connect4State.Rows ||
                    prevCol < 0 || prevCol >= Connect4State.Cols)
                    break;
                // The just-dropped cell is virtually filled with `side` from
                // the caller's perspective; for any other cell we read the
                // board's current value.
                var prevSide = (prevRow == row && prevCol == col)
                    ? side
                    : board.CellAt(prevRow, prevCol);
                if (prevSide != side) break;
                startRow = prevRow;
                startCol = prevCol;
            }

            // Walk forwards counting consecutive same-side cells.
            var runLength = 0;
            var r = startRow;
            var c = startCol;
            while (r >= 0 && r < Connect4State.Rows && c >= 0 && c < Connect4State.Cols)
            {
                var here = (r == row && c == col) ? side : board.CellAt(r, c);
                if (here != side) break;
                runLength++;
                r += dr;
                c += dc;
            }

            if (runLength >= RunLength)
            {
                var line = new Connect4Coordinate[RunLength];
                for (var i = 0; i < RunLength; i++)
                {
                    line[i] = new Connect4Coordinate(startRow + dr * i, startCol + dc * i);
                }
                return line;
            }
        }
        return null;
    }

    /// <summary>Wire shape of Connect 4's serialized state. Per-game and
    /// opaque to the platform.</summary>
    private sealed record StatePayload(
        int Rows,
        int Cols,
        IReadOnlyList<string?> Cells,
        Connect4Coordinate? LastMove = null,
        IReadOnlyList<Connect4Coordinate>? WinningLine = null);
}
