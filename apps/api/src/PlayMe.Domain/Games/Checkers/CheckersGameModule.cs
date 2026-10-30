using System.Text.Json;
using System.Text.Json.Serialization;
using PlayMe.Domain.Platform;

namespace PlayMe.Domain.Games.Checkers;

/// <summary>
/// Russian draughts (шашки) on an 8×8 board, play on the dark squares.
/// Men step one square diagonally forward and capture forward or backward;
/// kings fly any distance along a diagonal. Capturing is mandatory with
/// free choice among captures (no majority rule); a capture that leaves the
/// same piece a further capture must be continued; captured pieces leave
/// the board only when the chain ends (Turkish strike); a man promoting
/// mid-chain continues as a king. White moves first
/// (<see href="../../../../../docs/games/checkers.md">games/checkers.md</see>).
///
/// <para>
/// A move is one step or one jump. When a jump leaves the piece a further
/// capture, the module retains the mover's turn via
/// <see cref="MoveResult.KeepTurn"/> (the Sprint 10 seam B used by Sea
/// Battle and Reversi) and records the obligation in
/// <see cref="CheckersState.Chain"/>. Win and draw rules are evaluated only
/// when a turn ends, never mid-chain. The platform sees none of this
/// vocabulary (CLAUDE.md §7 "Platform thinness").
/// </para>
/// </summary>
public sealed class CheckersGameModule : IGameModule
{
    public static readonly GameId GameId = new("checkers");

    private static readonly string[] ValidSidesArray = { CheckersSides.White, CheckersSides.Black };

    /// <summary>The four diagonal directions (delta-row, delta-col).</summary>
    private static readonly (int DRow, int DCol)[] Diagonals =
    {
        (-1, -1), (-1, 1),
        (1, -1), (1, 1),
    };

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public GameId Id => GameId;

    public IReadOnlyList<string> ValidSides => ValidSidesArray;

    public string FirstMoveSide => CheckersSides.White;

    // Fixed per-side budget; Checkers has no per-room options.
    public TimeSpan ClockBudgetFor(JsonElement? options) => TimeSpan.FromMinutes(10);

    public string OtherSide(string side) => side switch
    {
        CheckersSides.White => CheckersSides.Black,
        CheckersSides.Black => CheckersSides.White,
        _ => throw new ArgumentException($"Unknown side '{side}'.", nameof(side)),
    };

    public string? ValidateOptions(JsonElement? options) =>
        options is null ? null : "errors.config.invalidGameOptions";

    public IGameState NewMatch(JsonElement? options)
    {
        var cells = new string?[CheckersState.CellCount];
        for (var row = 0; row < CheckersState.Size; row++)
        {
            for (var col = 0; col < CheckersState.Size; col++)
            {
                if (!CheckersState.IsDarkSquare(row, col)) continue;
                if (row <= 2) cells[CheckersState.IndexOf(row, col)] = CheckersPieces.BlackMan;
                else if (row >= 5) cells[CheckersState.IndexOf(row, col)] = CheckersPieces.WhiteMan;
            }
        }

        return new CheckersState(
            cells,
            CheckersSides.White,
            moveCount: 0,
            lastMove: null,
            chain: null,
            legalMoves: LegalMovesFor(cells, CheckersSides.White, chain: null),
            kingMovePlies: 0,
            history: Array.Empty<string>());
    }

    public MoveResult ApplyMove(IGameState state, string side, GameMove move)
    {
        var board = Cast(state);
        if (move is not CheckersMove checkersMove)
        {
            throw new ArgumentException(
                $"Expected {nameof(CheckersMove)}, got {move.GetType().Name}.", nameof(move));
        }
        if (side != CheckersSides.White && side != CheckersSides.Black)
        {
            throw new ArgumentException($"Unknown side '{side}'.", nameof(side));
        }

        var from = checkersMove.From;
        var to = checkersMove.To;
        if (!InBounds(from.Row, from.Col) || !InBounds(to.Row, to.Col))
        {
            return MoveResult.Reject(CheckersErrors.OutOfBounds);
        }
        if (CheckersPieces.SideOf(board.CellAt(from)) != side)
        {
            return MoveResult.Reject(CheckersErrors.NotYourPiece);
        }
        if (board.Chain is { } openChain && openChain.At != from)
        {
            return MoveResult.Reject(CheckersErrors.MustContinueCapture);
        }

        var cells = CopyCells(board.Cells);
        var legal = LegalMovesFor(cells, side, board.Chain);
        var chosen = Find(legal, from, to);
        if (chosen is null)
        {
            return MoveResult.Reject(ClassifyRejection(cells, side, from, to, board.Chain));
        }

        return chosen.Captured is null
            ? ApplyStep(board, cells, side, chosen)
            : ApplyJump(board, cells, side, chosen);
    }

    private MoveResult ApplyStep(CheckersState board, string?[] cells, string side, CheckersMoveInfo step)
    {
        var piece = cells[CheckersState.IndexOf(step.From)]!;
        var wasKing = CheckersPieces.IsKing(piece);
        cells[CheckersState.IndexOf(step.To)] = Promote(piece, side, step.To.Row);
        cells[CheckersState.IndexOf(step.From)] = null;

        // A man move is irreversible; a king move counts toward the
        // king-shuffling draw and keeps the repetition history alive.
        return EndTurn(board, cells, side, step, irreversible: !wasKing);
    }

    private MoveResult ApplyJump(CheckersState board, string?[] cells, string side, CheckersMoveInfo jump)
    {
        var piece = cells[CheckersState.IndexOf(jump.From)]!;
        cells[CheckersState.IndexOf(jump.To)] = Promote(piece, side, jump.To.Row);
        cells[CheckersState.IndexOf(jump.From)] = null;

        var captured = new List<CheckersCoordinate>(board.Chain?.Captured ?? Array.Empty<CheckersCoordinate>())
        {
            jump.Captured!.Value,
        };

        // Turkish strike: the jumped pieces stay on the board (still
        // blocking, no longer capturable) until the chain ends.
        var continuation = CapturesFrom(cells, jump.To, side, PendingMask(captured));
        if (continuation.Count > 0)
        {
            var midChain = new CheckersState(
                cells,
                side,
                board.MoveCount,
                lastMove: jump,
                chain: new CheckersChain(jump.To, captured),
                legalMoves: continuation,
                kingMovePlies: board.KingMovePlies,
                history: board.History);
            return MoveResult.Accept(midChain, keepTurn: true);
        }

        foreach (var at in captured)
        {
            cells[CheckersState.IndexOf(at)] = null;
        }
        return EndTurn(board, cells, side, jump, irreversible: true);
    }

    /// <summary>
    /// Close the mover's turn: update the draw counters, then decide whether
    /// the match ends — the opponent has no legal move (no pieces, or all
    /// blocked) → the mover wins; third occurrence of the position → draw;
    /// 30 king-only capture-free plies → draw — or play passes normally.
    /// Every turn-ending change goes through here, so a side with nothing
    /// to play is always detected at the move that stranded it.
    /// </summary>
    private MoveResult EndTurn(
        CheckersState board,
        string?[] cells,
        string mover,
        CheckersMoveInfo lastMove,
        bool irreversible)
    {
        var next = OtherSide(mover);
        var moveCount = board.MoveCount + 1;
        var kingMovePlies = irreversible ? 0 : board.KingMovePlies + 1;
        var key = PositionKey(cells, next);
        var history = irreversible
            ? new List<string> { key }
            : new List<string>(board.History) { key };
        var nextLegal = LegalMovesFor(cells, next, chain: null);

        if (nextLegal.Count == 0)
        {
            var won = new CheckersState(
                cells, next, moveCount, lastMove, chain: null, nextLegal, kingMovePlies, history);
            return MoveResult.Accept(won, new Win(mover));
        }

        var occurrences = 0;
        foreach (var seen in history)
        {
            if (seen == key) occurrences++;
        }
        if (occurrences >= CheckersState.RepetitionLimit)
        {
            var drawn = new CheckersState(
                cells, next, moveCount, lastMove, chain: null, nextLegal, kingMovePlies, history,
                CheckersDrawReasons.Repetition);
            return MoveResult.Accept(drawn, new Draw());
        }
        if (kingMovePlies >= CheckersState.KingMovePlyLimit)
        {
            var drawn = new CheckersState(
                cells, next, moveCount, lastMove, chain: null, nextLegal, kingMovePlies, history,
                CheckersDrawReasons.KingMoves);
            return MoveResult.Accept(drawn, new Draw());
        }

        var normal = new CheckersState(
            cells, next, moveCount, lastMove, chain: null, nextLegal, kingMovePlies, history);
        return MoveResult.Accept(normal);
    }

    /// <summary>
    /// Explain why (<paramref name="from"/>, <paramref name="to"/>) is not
    /// among the legal moves. A capture landing the king rule filtered out,
    /// or any non-capture while a chain is open, is "must continue
    /// capturing"; a plain step while a capture exists elsewhere is
    /// "capture mandatory"; anything else is simply unreachable.
    /// </summary>
    private static string ClassifyRejection(
        string?[] cells, string side, CheckersCoordinate from, CheckersCoordinate to, CheckersChain? chain)
    {
        var raw = RawCapturesFrom(cells, from, side, PendingMask(chain?.Captured));
        if (Find(raw, from, to) is not null)
        {
            return CheckersErrors.MustContinueCapture;
        }
        if (Find(StepsFrom(cells, from, side), from, to) is not null)
        {
            return chain is null ? CheckersErrors.CaptureMandatory : CheckersErrors.MustContinueCapture;
        }
        return CheckersErrors.IllegalMove;
    }

    // --- Rules -------------------------------------------------------------

    /// <summary>
    /// Every legal move for <paramref name="side"/>. Mid-chain: only
    /// captures by the chaining piece. Otherwise: all captures if any piece
    /// has one (free choice, no majority rule), else all steps.
    /// </summary>
    private static List<CheckersMoveInfo> LegalMovesFor(string?[] cells, string side, CheckersChain? chain)
    {
        if (chain is not null)
        {
            return CapturesFrom(cells, chain.At, side, PendingMask(chain.Captured));
        }

        var noPending = new bool[CheckersState.CellCount];
        var captures = new List<CheckersMoveInfo>();
        for (var row = 0; row < CheckersState.Size; row++)
        {
            for (var col = 0; col < CheckersState.Size; col++)
            {
                if (CheckersPieces.SideOf(cells[CheckersState.IndexOf(row, col)]) != side) continue;
                captures.AddRange(CapturesFrom(cells, new CheckersCoordinate(row, col), side, noPending));
            }
        }
        if (captures.Count > 0)
        {
            return captures;
        }

        var steps = new List<CheckersMoveInfo>();
        for (var row = 0; row < CheckersState.Size; row++)
        {
            for (var col = 0; col < CheckersState.Size; col++)
            {
                if (CheckersPieces.SideOf(cells[CheckersState.IndexOf(row, col)]) != side) continue;
                steps.AddRange(StepsFrom(cells, new CheckersCoordinate(row, col), side));
            }
        }
        return steps;
    }

    /// <summary>
    /// Captures available to the piece at <paramref name="from"/>, with the
    /// king landing rule applied: among the landing squares beyond one
    /// captured piece, if any allows a further capture only those are legal.
    /// </summary>
    private static List<CheckersMoveInfo> CapturesFrom(
        string?[] cells, CheckersCoordinate from, string side, bool[] pending)
    {
        var raw = RawCapturesFrom(cells, from, side, pending);
        var piece = cells[CheckersState.IndexOf(from)];
        if (raw.Count == 0 || !CheckersPieces.IsKing(piece))
        {
            return raw;
        }

        var result = new List<CheckersMoveInfo>(raw.Count);
        var continuing = new List<CheckersMoveInfo>();
        foreach (var over in DistinctCaptured(raw))
        {
            continuing.Clear();
            foreach (var capture in raw)
            {
                if (capture.Captured != over) continue;
                if (CanCaptureAfter(cells, capture, side, pending))
                {
                    continuing.Add(capture);
                }
            }
            if (continuing.Count > 0)
            {
                result.AddRange(continuing);
            }
            else
            {
                foreach (var capture in raw)
                {
                    if (capture.Captured == over) result.Add(capture);
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Would the king have a further capture after making <paramref name="capture"/>?
    /// Evaluated on a hypothetical board with the king moved and the jumped
    /// piece marked pending.
    /// </summary>
    private static bool CanCaptureAfter(
        string?[] cells, CheckersMoveInfo capture, string side, bool[] pending)
    {
        var hypothetical = (string?[])cells.Clone();
        hypothetical[CheckersState.IndexOf(capture.To)] = hypothetical[CheckersState.IndexOf(capture.From)];
        hypothetical[CheckersState.IndexOf(capture.From)] = null;
        var pendingAfter = (bool[])pending.Clone();
        pendingAfter[CheckersState.IndexOf(capture.Captured!.Value)] = true;
        return RawCapturesFrom(hypothetical, capture.To, side, pendingAfter).Count > 0;
    }

    /// <summary>
    /// Geometric captures for the piece at <paramref name="from"/>, ignoring
    /// the king landing rule. A man jumps an adjacent enemy onto the empty
    /// square beyond, in any of the four directions. A king runs along a
    /// diagonal through empty squares to the first piece; if it is an enemy
    /// that is not pending and at least one empty square follows, every
    /// empty square beyond is a landing. Pending pieces block and are never
    /// capturable.
    /// </summary>
    private static List<CheckersMoveInfo> RawCapturesFrom(
        string?[] cells, CheckersCoordinate from, string side, bool[] pending)
    {
        var result = new List<CheckersMoveInfo>();
        var piece = cells[CheckersState.IndexOf(from)];
        if (CheckersPieces.SideOf(piece) != side)
        {
            return result;
        }
        var isKing = CheckersPieces.IsKing(piece);

        foreach (var (dRow, dCol) in Diagonals)
        {
            var row = from.Row + dRow;
            var col = from.Col + dCol;
            if (isKing)
            {
                while (InBounds(row, col) && cells[CheckersState.IndexOf(row, col)] is null)
                {
                    row += dRow;
                    col += dCol;
                }
            }
            if (!InBounds(row, col)) continue;
            var overIndex = CheckersState.IndexOf(row, col);
            if (!IsEnemy(cells[overIndex], side) || pending[overIndex]) continue;
            var over = new CheckersCoordinate(row, col);

            row += dRow;
            col += dCol;
            if (!isKing)
            {
                if (InBounds(row, col) && cells[CheckersState.IndexOf(row, col)] is null)
                {
                    result.Add(new CheckersMoveInfo(from, new CheckersCoordinate(row, col), over));
                }
                continue;
            }
            while (InBounds(row, col) && cells[CheckersState.IndexOf(row, col)] is null)
            {
                result.Add(new CheckersMoveInfo(from, new CheckersCoordinate(row, col), over));
                row += dRow;
                col += dCol;
            }
        }
        return result;
    }

    /// <summary>
    /// Non-capturing moves for the piece at <paramref name="from"/>: a man
    /// one square diagonally forward, a king any distance along any diagonal.
    /// </summary>
    private static List<CheckersMoveInfo> StepsFrom(string?[] cells, CheckersCoordinate from, string side)
    {
        var result = new List<CheckersMoveInfo>();
        var piece = cells[CheckersState.IndexOf(from)];
        if (CheckersPieces.SideOf(piece) != side)
        {
            return result;
        }
        var isKing = CheckersPieces.IsKing(piece);
        var forward = ForwardDirection(side);

        foreach (var (dRow, dCol) in Diagonals)
        {
            if (!isKing && dRow != forward) continue;
            var row = from.Row + dRow;
            var col = from.Col + dCol;
            while (InBounds(row, col) && cells[CheckersState.IndexOf(row, col)] is null)
            {
                result.Add(new CheckersMoveInfo(from, new CheckersCoordinate(row, col)));
                if (!isKing) break;
                row += dRow;
                col += dCol;
            }
        }
        return result;
    }

    // --- Serialization -----------------------------------------------------

    public string Serialize(IGameState state)
    {
        var board = Cast(state);
        var payload = new StatePayload(
            board.Cells,
            board.SideToMove,
            board.MoveCount,
            board.LastMove,
            board.Chain,
            board.LegalMoves,
            board.WhiteCount,
            board.BlackCount,
            board.KingMovePlies,
            board.History,
            board.DrawReason);
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
            throw new ArgumentException("Failed to parse Checkers state.", nameof(serialized), e);
        }
        if (payload is null)
        {
            throw new ArgumentException("Checkers state blob was null.", nameof(serialized));
        }
        if (payload.Cells is null || payload.Cells.Count != CheckersState.CellCount || payload.SideToMove is null)
        {
            throw new ArgumentException(
                $"Checkers state shape mismatch: cells={payload.Cells?.Count ?? 0}, side={payload.SideToMove}.",
                nameof(serialized));
        }

        try
        {
            var cells = CopyCells(payload.Cells);
            // LegalMoves is derived: recompute rather than trust the blob.
            var legal = LegalMovesFor(cells, payload.SideToMove, payload.Chain);
            return new CheckersState(
                cells,
                payload.SideToMove,
                payload.MoveCount,
                payload.LastMove,
                payload.Chain,
                legal,
                payload.KingMovePlies,
                payload.History ?? Array.Empty<string>(),
                payload.DrawReason);
        }
        catch (ArgumentException e)
        {
            throw new ArgumentException("Checkers state blob is invalid.", nameof(serialized), e);
        }
    }

    // --- Helpers -----------------------------------------------------------

    private static CheckersState Cast(IGameState state)
    {
        if (state is not CheckersState board)
        {
            throw new ArgumentException(
                $"Expected {nameof(CheckersState)}, got {state.GetType().Name}.", nameof(state));
        }
        return board;
    }

    private static bool InBounds(int row, int col) =>
        row >= 0 && row < CheckersState.Size && col >= 0 && col < CheckersState.Size;

    private static bool IsEnemy(string? piece, string side)
    {
        var owner = CheckersPieces.SideOf(piece);
        return owner is not null && owner != side;
    }

    /// <summary>White advances toward row 0, black toward row 7.</summary>
    private static int ForwardDirection(string side) => side == CheckersSides.White ? -1 : 1;

    private static int FarRank(string side) => side == CheckersSides.White ? 0 : CheckersState.Size - 1;

    private static string Promote(string piece, string side, int landingRow) =>
        !CheckersPieces.IsKing(piece) && landingRow == FarRank(side) ? CheckersPieces.KingOf(side) : piece;

    private static CheckersMoveInfo? Find(List<CheckersMoveInfo> moves, CheckersCoordinate from, CheckersCoordinate to)
    {
        foreach (var move in moves)
        {
            if (move.From == from && move.To == to) return move;
        }
        return null;
    }

    private static List<CheckersCoordinate> DistinctCaptured(List<CheckersMoveInfo> captures)
    {
        var result = new List<CheckersCoordinate>();
        foreach (var capture in captures)
        {
            var over = capture.Captured!.Value;
            if (!result.Contains(over)) result.Add(over);
        }
        return result;
    }

    private static bool[] PendingMask(IReadOnlyList<CheckersCoordinate>? pending)
    {
        var mask = new bool[CheckersState.CellCount];
        if (pending is null) return mask;
        foreach (var at in pending)
        {
            mask[CheckersState.IndexOf(at)] = true;
        }
        return mask;
    }

    private static string?[] CopyCells(IReadOnlyList<string?> source)
    {
        var copy = new string?[CheckersState.CellCount];
        for (var i = 0; i < CheckersState.CellCount; i++)
        {
            copy[i] = source[i];
        }
        return copy;
    }

    /// <summary>
    /// Compact position key for the repetition rule: one character per cell
    /// plus the side to move. Module-internal; carried in the state's
    /// <see cref="CheckersState.History"/>.
    /// </summary>
    private static string PositionKey(string?[] cells, string sideToMove)
    {
        var chars = new char[CheckersState.CellCount + 1];
        for (var i = 0; i < CheckersState.CellCount; i++)
        {
            chars[i] = cells[i] switch
            {
                null => '.',
                CheckersPieces.WhiteMan => 'w',
                CheckersPieces.WhiteKing => 'W',
                CheckersPieces.BlackMan => 'b',
                CheckersPieces.BlackKing => 'B',
                _ => '?',
            };
        }
        chars[CheckersState.CellCount] = sideToMove == CheckersSides.White ? 'w' : 'b';
        return new string(chars);
    }

    /// <summary>Wire shape of the serialized state. Per-game and opaque to the platform.</summary>
    private sealed record StatePayload(
        IReadOnlyList<string?> Cells,
        string SideToMove,
        int MoveCount,
        CheckersMoveInfo? LastMove,
        CheckersChain? Chain,
        IReadOnlyList<CheckersMoveInfo>? LegalMoves,
        int WhiteCount,
        int BlackCount,
        int KingMovePlies,
        IReadOnlyList<string>? History,
        string? DrawReason);
}
