using System.Text.Json;
using System.Text.Json.Serialization;
using PlayMe.Domain.Platform;

namespace PlayMe.Domain.Games.TicTacToe;

/// <summary>
/// Unified Tic-Tac-Toe rules engine (Sprint 9 PR1b). Replaces the three
/// per-size legacy modules (<c>tictactoe-3x3</c>, <c>-6x6</c>, <c>-9x9</c>)
/// with a single module whose per-room <c>gameOptions</c> blob carries the
/// host-chosen <c>boardSize</c> (3, 6, or 9). The win length is derived
/// deterministically: 3→3, 6→4, 9→5. Players alternate placing X/O; the
/// first to align <see cref="TicTacToeState.WinLength"/> or more consecutive
/// marks (row, column, or either diagonal) wins. Board fills with no line →
/// draw. X moves first. No wraparound.
/// <para>
/// All per-game vocabulary (sides, reject keys, state shape, options shape)
/// lives inside this module. The platform never inspects any of it
/// (CLAUDE.md §7 "Platform thinness").
/// </para>
/// </summary>
public sealed class TicTacToeGameModule : IGameModule
{
    public static readonly GameId GameId = new("tictactoe");

    /// <summary>Allowed board sizes for the <c>boardSize</c> option.</summary>
    public static readonly IReadOnlyList<int> AllowedBoardSizes = new[] { 3, 6, 9 };

    private static readonly string[] ValidSidesArray = { TicTacToeSides.X, TicTacToeSides.O };

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

    public string FirstMoveSide => TicTacToeSides.X;

    /// <summary>
    /// Baseline budget used when the platform falls back to a single
    /// per-game value (rematch clock today, until per-room time limits
    /// land — see <see cref="PlatformConstants.GraceForBudget"/>). The
    /// configure page picks size-specific defaults UI-side; this value
    /// only governs scenarios that bypass UI selection.
    /// </summary>
    public TimeSpan DefaultClockBudget { get; } = TimeSpan.FromMinutes(3);

    public string OtherSide(string side) => side switch
    {
        TicTacToeSides.X => TicTacToeSides.O,
        TicTacToeSides.O => TicTacToeSides.X,
        _ => throw new ArgumentException($"Unknown side '{side}'.", nameof(side)),
    };

    public string? ValidateOptions(JsonElement? options)
    {
        // The unified module REQUIRES options — without boardSize there is
        // no board to lay out. Reject null up front.
        if (options is null) return TicTacToeErrors.ConfigInvalidGameOptions;

        var element = options.Value;
        if (element.ValueKind != JsonValueKind.Object)
        {
            return TicTacToeErrors.ConfigInvalidGameOptions;
        }
        if (!element.TryGetProperty("boardSize", out var sizeEl) ||
            sizeEl.ValueKind != JsonValueKind.Number ||
            !sizeEl.TryGetInt32(out var boardSize))
        {
            return TicTacToeErrors.ConfigInvalidGameOptions;
        }
        if (!AllowedBoardSizes.Contains(boardSize))
        {
            return TicTacToeErrors.ConfigInvalidGameOptions;
        }
        return null;
    }

    public IGameState NewMatch(JsonElement? options)
    {
        // Caller (handler) should have already validated options. We
        // re-extract defensively rather than re-validate — invariants on
        // boardSize hold by construction by the time NewMatch runs.
        var boardSize = ExtractBoardSize(options);
        var winLength = WinLengthFor(boardSize);
        return new TicTacToeState(boardSize, winLength);
    }

    public MoveResult ApplyMove(IGameState state, string side, GameMove move)
    {
        if (state is not TicTacToeState board)
        {
            throw new ArgumentException(
                $"Expected {nameof(TicTacToeState)}, got {state.GetType().Name}.", nameof(state));
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
        if (cell < 0 || cell >= board.CellCount)
        {
            return MoveResult.Reject(TicTacToeErrors.IllegalCell);
        }
        if (!board.IsCellEmpty(cell))
        {
            return MoveResult.Reject(TicTacToeErrors.CellOccupied);
        }

        // Probe for a winning run before constructing the new state so the
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
        if (state is not TicTacToeState board)
        {
            throw new ArgumentException(
                $"Expected {nameof(TicTacToeState)}, got {state.GetType().Name}.", nameof(state));
        }
        var payload = new StatePayload(
            board.BoardSize,
            board.BoardSize,
            board.WinLength,
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
            throw new ArgumentException("Failed to parse TicTacToe state.", nameof(serialized), e);
        }
        if (payload is null)
        {
            throw new ArgumentException("TicTacToe state blob was null.", nameof(serialized));
        }
        if (payload.Rows != payload.Cols ||
            !AllowedBoardSizes.Contains(payload.Rows) ||
            payload.Cells is null ||
            payload.Cells.Count != payload.Rows * payload.Cols)
        {
            throw new ArgumentException(
                $"TicTacToe state shape mismatch: rows={payload.Rows}, cols={payload.Cols}, cells={payload.Cells?.Count ?? 0}.",
                nameof(serialized));
        }
        if (payload.WinLength != WinLengthFor(payload.Rows))
        {
            throw new ArgumentException(
                $"TicTacToe winLength {payload.WinLength} does not match board size {payload.Rows}.",
                nameof(serialized));
        }
        return new TicTacToeState(
            payload.Rows,
            payload.WinLength,
            payload.Cells,
            payload.LastMove,
            payload.WinningLine);
    }

    /// <summary>
    /// Derive the win length from the board size — the canonical mapping
    /// per docs/games/tictactoe.md: 3→3, 6→4, 9→5.
    /// </summary>
    public static int WinLengthFor(int boardSize) => boardSize switch
    {
        3 => 3,
        6 => 4,
        9 => 5,
        _ => throw new ArgumentOutOfRangeException(nameof(boardSize),
            $"No win-length mapping for boardSize {boardSize}."),
    };

    private static int ExtractBoardSize(JsonElement? options)
    {
        if (options is null) throw new InvalidOperationException(
            "TicTacToe NewMatch requires options; ValidateOptions should have rejected null.");
        var element = options.Value;
        if (!element.TryGetProperty("boardSize", out var sizeEl) ||
            !sizeEl.TryGetInt32(out var boardSize) ||
            !AllowedBoardSizes.Contains(boardSize))
        {
            throw new InvalidOperationException(
                "TicTacToe NewMatch called with unvalidated options; ValidateOptions should have rejected.");
        }
        return boardSize;
    }

    /// <summary>
    /// Return the run through <paramref name="cell"/> that <paramref name="side"/>
    /// has just completed (length ≥ <see cref="TicTacToeState.WinLength"/>),
    /// or null if no win. We only inspect lines containing the latest cell
    /// — every earlier winning configuration would already have ended the
    /// match. For each of the four directions we walk backwards to the
    /// run's anchor cell, then forwards counting consecutive same-side
    /// cells; a run ≥ the win length is reported in full so 5-, 6-, …,
    /// up-to-boardSize-in-a-row are all valid wins highlighted in full.
    /// </summary>
    private static TicTacToeCoordinate[]? FindWinningLineFor(
        TicTacToeState board, int cell, string side)
    {
        var size = board.BoardSize;
        var row = cell / size;
        var col = cell % size;

        foreach (var (dr, dc) in Directions)
        {
            var startRow = row;
            var startCol = col;
            // Walk backwards along (-dr, -dc) to the run's anchor cell. The
            // just-played cell is virtually filled with `side`; any other
            // cell we read from the board.
            while (true)
            {
                var prevRow = startRow - dr;
                var prevCol = startCol - dc;
                if (prevRow < 0 || prevRow >= size || prevCol < 0 || prevCol >= size)
                    break;
                var prevSide = (prevRow == row && prevCol == col)
                    ? side
                    : board.CellAt(prevRow * size + prevCol);
                if (prevSide != side) break;
                startRow = prevRow;
                startCol = prevCol;
            }

            // Walk forwards counting consecutive same-side cells.
            var runLength = 0;
            var r = startRow;
            var c = startCol;
            while (r >= 0 && r < size && c >= 0 && c < size)
            {
                var here = (r == row && c == col)
                    ? side
                    : board.CellAt(r * size + c);
                if (here != side) break;
                runLength++;
                r += dr;
                c += dc;
            }

            if (runLength >= board.WinLength)
            {
                var line = new TicTacToeCoordinate[runLength];
                for (var i = 0; i < runLength; i++)
                {
                    line[i] = new TicTacToeCoordinate(startRow + dr * i, startCol + dc * i);
                }
                return line;
            }
        }
        return null;
    }

    /// <summary>Wire shape of the unified TTT's serialized state. Per-game
    /// and opaque to the platform.</summary>
    private sealed record StatePayload(
        int Rows,
        int Cols,
        int WinLength,
        IReadOnlyList<string?> Cells,
        int? LastMove = null,
        IReadOnlyList<TicTacToeCoordinate>? WinningLine = null);
}
