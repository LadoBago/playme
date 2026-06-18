using System.Text.Json;
using System.Text.Json.Serialization;
using PlayMe.Domain.Platform;

namespace PlayMe.Domain.Games.Reversi;

/// <summary>
/// Classic Reversi rules: 8×8 board, two sides dark and light placing
/// two-sided discs. The first four placements are restricted to the central
/// 2×2 squares (rows 3–4 × cols 3–4) and flip nothing — the classic free
/// opening; standard bracketing play begins on move 5. Dark moves first
/// (see <see href="../../../../../docs/games/reversi.md">games/reversi.md</see>).
///
/// <para>
/// Forced skip: when a placement leaves the opponent without a legal move
/// (and the mover still has one), the module retains the mover's turn via
/// <see cref="MoveResult.KeepTurn"/> (seam B) and records the stranded
/// side in <see cref="ReversiState.SkippedSide"/> so the renderer can
/// toast both seats. The skip is resolved synchronously on the server —
/// there is no pass move, no client round-trip, and no pass vocabulary
/// for the platform to see (CLAUDE.md §7 "Platform thinness"). A
/// placement that strands <em>both</em> sides ends the match immediately.
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

    // Fixed per-side budget; Reversi has no per-room options.
    public TimeSpan ClockBudgetFor(JsonElement? options) => TimeSpan.FromMinutes(10);

    public string OtherSide(string side) => side switch
    {
        ReversiSides.Dark => ReversiSides.Light,
        ReversiSides.Light => ReversiSides.Dark,
        _ => throw new ArgumentException($"Unknown side '{side}'.", nameof(side)),
    };

    public string? ValidateOptions(JsonElement? options) =>
        options is null ? null : "errors.config.invalidGameOptions";

    public IGameState NewMatch(JsonElement? options) => new ReversiState();

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
            flippedLastTurn: flipped,
            sideThatJustMoved: side);
    }

    /// <summary>
    /// Build the post-placement state and decide what follows: the match
    /// ends (board full, or neither side can move), the opponent's turn is
    /// skipped (they have no legal move — the mover keeps the turn via
    /// <see cref="MoveResult.KeepTurn"/>), or play alternates normally.
    /// Every board change goes through here, so a board where nobody can
    /// move is always detected at the placement that produced it — there is
    /// no reachable "both stuck" state that survives to a next turn.
    /// </summary>
    private MoveResult FinalizeMove(
        string?[] cells,
        int moveCount,
        ReversiCoordinate? lastPlacement,
        IReadOnlyList<ReversiCoordinate> flippedLastTurn,
        string sideThatJustMoved)
    {
        var nextSide = OtherSide(sideThatJustMoved);
        var inOpening = moveCount < ReversiState.OpeningMoveCount;

        if (CellsFull(cells))
        {
            var terminal = new ReversiState(cells, moveCount, lastPlacement, flippedLastTurn);
            return MoveResult.Accept(terminal, OutcomeFromCounts(terminal.DarkCount, terminal.LightCount));
        }

        var nextSideCanMove = HasAnyLegalMove(cells, nextSide, inOpening);
        if (!nextSideCanMove)
        {
            // The opponent is stranded. If the mover is also stuck, nobody
            // can ever move again — end the match here.
            var sameSideCanMove = HasAnyLegalMove(cells, sideThatJustMoved, inOpening);
            if (!sameSideCanMove)
            {
                var terminal = new ReversiState(cells, moveCount, lastPlacement, flippedLastTurn);
                return MoveResult.Accept(terminal, OutcomeFromCounts(terminal.DarkCount, terminal.LightCount));
            }

            // Skip the opponent's turn: the mover keeps the move (seam B)
            // and SkippedSide drives the per-seat renderer toast.
            var stateSkipped = new ReversiState(
                cells, moveCount, lastPlacement, flippedLastTurn, skippedSide: nextSide);
            return MoveResult.Accept(stateSkipped, keepTurn: true);
        }

        var stateNormal = new ReversiState(cells, moveCount, lastPlacement, flippedLastTurn);
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
            board.FlippedLastTurn,
            board.SkippedSide,
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
            payload.FlippedLastTurn ?? Array.Empty<ReversiCoordinate>(),
            payload.SkippedSide);
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
    private static bool HasAnyLegalMove(string?[] cells, string side, bool inOpening)
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
        string?[] cells, int row, int col, string side)
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
        IReadOnlyList<ReversiCoordinate>? FlippedLastTurn,
        string? SkippedSide = null,
        int DarkCount = 0,
        int LightCount = 0);
}
