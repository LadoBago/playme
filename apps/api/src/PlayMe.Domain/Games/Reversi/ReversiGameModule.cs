using System.Text.Json;
using System.Text.Json.Serialization;
using PlayMe.Domain.Platform;

namespace PlayMe.Domain.Games.Reversi;

/// <summary>
/// Classic Reversi rules: 8×8 board, two sides dark and light placing
/// two-sided discs. The first four placements are restricted to the central
/// 2×2 squares (rows 3–4 × cols 3–4) and flip nothing — the classic free
/// opening; standard bracketing play begins on move 5. Dark moves first
/// (see <see href="../../../../../docs/platform-and-games.md">platform-and-games.md §2.1</see>).
///
/// <para>
/// Auto-pass: when the next side-to-move has no legal placement, the
/// module sets <see cref="ReversiState.MustPassSide"/> on the new state.
/// The Reversi web renderer reads the flag and submits a synthetic
/// <see cref="ReversiPass"/> move; the server re-validates that the side
/// truly has no legal moves and rejects the pass otherwise. The platform
/// never sees pass vocabulary — it routes the move opaquely via
/// <see cref="IGameModule.ApplyMove"/> (CLAUDE.md §7 "Platform thinness").
/// </para>
/// </summary>
public sealed class ReversiGameModule : IGameModule
{
    public static readonly GameId GameId = new("reversi");

    private static readonly string[] ValidSidesArray = { ReversiSides.Dark, ReversiSides.Light };

    /// <summary>
    /// Eight neighbour vectors (delta-row, delta-col) covering every line a
    /// placed disc can bracket along: cardinal + diagonal.
    /// </summary>
    private static readonly (int DRow, int DCol)[] Directions =
    {
        (-1, -1), (-1, 0), (-1, 1),
        (0, -1),           (0, 1),
        (1, -1),  (1, 0),  (1, 1),
    };

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public GameId Id => GameId;

    public IReadOnlyList<string> ValidSides => ValidSidesArray;

    public string FirstMoveSide => ReversiSides.Dark;

    public TimeSpan DefaultClockBudget { get; } = TimeSpan.FromMinutes(10);

    public string OtherSide(string side) => side switch
    {
        ReversiSides.Dark => ReversiSides.Light,
        ReversiSides.Light => ReversiSides.Dark,
        _ => throw new ArgumentException($"Unknown side '{side}'.", nameof(side)),
    };

    public IGameState NewMatch() => new ReversiState();

    public MoveResult ApplyMove(IGameState state, string side, GameMove move)
    {
        if (state is not ReversiState board)
        {
            throw new ArgumentException(
                $"Expected {nameof(ReversiState)}, got {state.GetType().Name}.", nameof(state));
        }
        if (move is not ReversiMove reversiMove)
        {
            throw new ArgumentException(
                $"Expected {nameof(ReversiMove)}, got {move.GetType().Name}.", nameof(move));
        }
        if (side != ReversiSides.Dark && side != ReversiSides.Light)
        {
            throw new ArgumentException($"Unknown side '{side}'.", nameof(side));
        }

        return reversiMove switch
        {
            ReversiPlacement placement => ApplyPlacement(board, side, placement),
            ReversiPass => ApplyPass(board, side),
            _ => throw new ArgumentException(
                $"Unknown Reversi move type {reversiMove.GetType().Name}.", nameof(move)),
        };
    }

    private MoveResult ApplyPlacement(ReversiState board, string side, ReversiPlacement placement)
    {
        var row = placement.Row;
        var col = placement.Col;

        if (!InBounds(row, col))
        {
            return MoveResult.Reject(ReversiErrors.OutOfBounds);
        }
        if (board.CellAt(row, col) is not null)
        {
            return MoveResult.Reject(ReversiErrors.CellOccupied);
        }

        IReadOnlyList<ReversiCoordinate> flipped;
        if (board.InOpening)
        {
            if (!IsCentralSquare(row, col))
            {
                return MoveResult.Reject(ReversiErrors.OpeningMustBeCentral);
            }
            flipped = Array.Empty<ReversiCoordinate>();
        }
        else
        {
            flipped = ComputeBracketedCells(board.Cells, row, col, side);
            if (flipped.Count == 0)
            {
                return MoveResult.Reject(ReversiErrors.MustBracket);
            }
        }

        var newCells = CopyCells(board.Cells);
        newCells[ReversiState.IndexOf(row, col)] = side;
        foreach (var flip in flipped)
        {
            newCells[ReversiState.IndexOf(flip.Row, flip.Col)] = side;
        }

        return FinalizeMove(
            cells: newCells,
            moveCount: board.MoveCount + 1,
            lastPlacement: new ReversiCoordinate(row, col),
            lastWasPass: false,
            flippedLastTurn: flipped,
            consecutivePasses: 0,
            sideThatJustMoved: side);
    }

    private MoveResult ApplyPass(ReversiState board, string side)
    {
        // Two-layer pass validation: (1) the previously published state's
        // MustPassSide flag, which the renderer keys off to auto-emit the
        // pass, must name this side; (2) re-check legality against the
        // board itself in case the flag is stale or the client is buggy/
        // malicious. Both must hold.
        if (board.MustPassSide != side)
        {
            return MoveResult.Reject(ReversiErrors.PassNotAllowed);
        }
        if (HasAnyLegalMove(board.Cells, side, board.InOpening))
        {
            return MoveResult.Reject(ReversiErrors.PassNotAllowed);
        }

        var newCells = CopyCells(board.Cells);

        return FinalizeMove(
            cells: newCells,
            moveCount: board.MoveCount + 1,
            lastPlacement: null,
            lastWasPass: true,
            flippedLastTurn: Array.Empty<ReversiCoordinate>(),
            consecutivePasses: board.ConsecutivePasses + 1,
            sideThatJustMoved: side);
    }

    /// <summary>
    /// Build the post-move state and decide whether the match ends here or
    /// the next side must auto-pass. The platform will unconditionally flip
    /// <c>Match.SideToMove</c> to <see cref="OtherSide"/>(<paramref name="sideThatJustMoved"/>);
    /// <see cref="ReversiState.MustPassSide"/> is set against that next side
    /// when needed.
    /// </summary>
    private MoveResult FinalizeMove(
        string?[] cells,
        int moveCount,
        ReversiCoordinate? lastPlacement,
        bool lastWasPass,
        IReadOnlyList<ReversiCoordinate> flippedLastTurn,
        int consecutivePasses,
        string sideThatJustMoved)
    {
        var nextSide = OtherSide(sideThatJustMoved);
        var inOpening = moveCount < ReversiState.OpeningMoveCount;

        if (consecutivePasses >= 2 || CellsFull(cells))
        {
            var terminal = new ReversiState(
                cells, moveCount, lastPlacement, lastWasPass, flippedLastTurn,
                consecutivePasses, mustPassSide: null);
            return MoveResult.Accept(terminal, OutcomeFromCounts(terminal.DarkCount, terminal.LightCount));
        }

        var nextSideCanMove = HasAnyLegalMove(cells, nextSide, inOpening);
        if (!nextSideCanMove)
        {
            // Forced pass for the next side. Short-circuit to terminal if
            // sideThatJustMoved is also stuck — avoids a two-round-trip
            // double auto-pass for a board where nobody can move.
            var sameSideCanMove = HasAnyLegalMove(cells, sideThatJustMoved, inOpening);
            if (!sameSideCanMove)
            {
                var terminal = new ReversiState(
                    cells, moveCount, lastPlacement, lastWasPass, flippedLastTurn,
                    consecutivePasses, mustPassSide: null);
                return MoveResult.Accept(terminal, OutcomeFromCounts(terminal.DarkCount, terminal.LightCount));
            }

            var stateForcedPass = new ReversiState(
                cells, moveCount, lastPlacement, lastWasPass, flippedLastTurn,
                consecutivePasses, mustPassSide: nextSide);
            return MoveResult.Accept(stateForcedPass);
        }

        var stateNormal = new ReversiState(
            cells, moveCount, lastPlacement, lastWasPass, flippedLastTurn,
            consecutivePasses, mustPassSide: null);
        return MoveResult.Accept(stateNormal);
    }

    public string Serialize(IGameState state)
    {
        if (state is not ReversiState board)
        {
            throw new ArgumentException(
                $"Expected {nameof(ReversiState)}, got {state.GetType().Name}.", nameof(state));
        }
        var payload = new StatePayload(
            ReversiState.Size,
            board.MoveCount,
            board.Cells,
            board.LastPlacement,
            board.LastWasPass,
            board.FlippedLastTurn,
            board.ConsecutivePasses,
            board.MustPassSide,
            board.DarkCount,
            board.LightCount);
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
            throw new ArgumentException("Failed to parse Reversi state.", nameof(serialized), e);
        }
        if (payload is null)
        {
            throw new ArgumentException("Reversi state blob was null.", nameof(serialized));
        }
        if (payload.Size != ReversiState.Size ||
            payload.Cells is null ||
            payload.Cells.Count != ReversiState.CellCount)
        {
            throw new ArgumentException(
                $"Reversi state shape mismatch: size={payload.Size}, cells={payload.Cells?.Count ?? 0}.",
                nameof(serialized));
        }
        return new ReversiState(
            payload.Cells,
            payload.MoveCount,
            payload.LastPlacement,
            payload.LastWasPass,
            payload.FlippedLastTurn ?? Array.Empty<ReversiCoordinate>(),
            payload.ConsecutivePasses,
            payload.MustPassSide);
    }

    private static bool InBounds(int row, int col) =>
        row >= 0 && row < ReversiState.Size && col >= 0 && col < ReversiState.Size;

    private static bool IsCentralSquare(int row, int col) =>
        (row == 3 || row == 4) && (col == 3 || col == 4);

    private static string?[] CopyCells(IReadOnlyList<string?> source)
    {
        var copy = new string?[ReversiState.CellCount];
        for (var i = 0; i < ReversiState.CellCount; i++)
        {
            copy[i] = source[i];
        }
        return copy;
    }

    private static bool CellsFull(string?[] cells)
    {
        for (var i = 0; i < cells.Length; i++)
        {
            if (cells[i] is null) return false;
        }
        return true;
    }

    private static Outcome OutcomeFromCounts(int darkCount, int lightCount)
    {
        if (darkCount > lightCount) return new Win(ReversiSides.Dark);
        if (lightCount > darkCount) return new Win(ReversiSides.Light);
        return new Draw();
    }

    /// <summary>
    /// Walk every direction from (<paramref name="row"/>, <paramref name="col"/>)
    /// collecting opponent discs that would flip if <paramref name="side"/>
    /// placed there. A direction contributes only if a run of ≥1 opposing
    /// disc terminates with one of the mover's own discs; runs ending in an
    /// empty cell or at the board edge contribute nothing.
    /// </summary>
    private static List<ReversiCoordinate> ComputeBracketedCells(
        IReadOnlyList<string?> cells, int row, int col, string side)
    {
        var other = side == ReversiSides.Dark ? ReversiSides.Light : ReversiSides.Dark;
        var result = new List<ReversiCoordinate>();
        foreach (var (dr, dc) in Directions)
        {
            var run = new List<ReversiCoordinate>();
            var r = row + dr;
            var c = col + dc;
            while (InBounds(r, c) && cells[ReversiState.IndexOf(r, c)] == other)
            {
                run.Add(new ReversiCoordinate(r, c));
                r += dr;
                c += dc;
            }
            if (run.Count > 0 && InBounds(r, c) && cells[ReversiState.IndexOf(r, c)] == side)
            {
                result.AddRange(run);
            }
        }
        return result;
    }

    /// <summary>
    /// True if <paramref name="side"/> has at least one legal placement on
    /// <paramref name="cells"/>. In the opening (first four placements),
    /// any empty central-2×2 square is legal. In standard play, the cell
    /// must additionally bracket at least one opponent disc.
    /// </summary>
    private static bool HasAnyLegalMove(IReadOnlyList<string?> cells, string side, bool inOpening)
    {
        if (inOpening)
        {
            for (var r = 3; r <= 4; r++)
            {
                for (var c = 3; c <= 4; c++)
                {
                    if (cells[ReversiState.IndexOf(r, c)] is null) return true;
                }
            }
            return false;
        }

        for (var r = 0; r < ReversiState.Size; r++)
        {
            for (var c = 0; c < ReversiState.Size; c++)
            {
                if (cells[ReversiState.IndexOf(r, c)] is not null) continue;
                if (BracketsAny(cells, r, c, side)) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// True if a placement at (<paramref name="row"/>, <paramref name="col"/>)
    /// by <paramref name="side"/> would flip at least one opponent disc.
    /// Short-circuits on the first directional bracket — faster than the
    /// full <see cref="ComputeBracketedCells"/> for legality checks.
    /// </summary>
    private static bool BracketsAny(
        IReadOnlyList<string?> cells, int row, int col, string side)
    {
        var other = side == ReversiSides.Dark ? ReversiSides.Light : ReversiSides.Dark;
        foreach (var (dr, dc) in Directions)
        {
            var r = row + dr;
            var c = col + dc;
            var captured = 0;
            while (InBounds(r, c) && cells[ReversiState.IndexOf(r, c)] == other)
            {
                captured++;
                r += dr;
                c += dc;
            }
            if (captured > 0 && InBounds(r, c) && cells[ReversiState.IndexOf(r, c)] == side)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Wire shape of Reversi's serialized state. Per-game and
    /// opaque to the platform.</summary>
    private sealed record StatePayload(
        int Size,
        int MoveCount,
        IReadOnlyList<string?> Cells,
        ReversiCoordinate? LastPlacement,
        bool LastWasPass,
        IReadOnlyList<ReversiCoordinate>? FlippedLastTurn,
        int ConsecutivePasses,
        string? MustPassSide,
        int DarkCount = 0,
        int LightCount = 0);
}
