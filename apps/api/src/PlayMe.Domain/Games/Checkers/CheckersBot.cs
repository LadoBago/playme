using PlayMe.Domain.Platform;

namespace PlayMe.Domain.Games.Checkers;

/// <summary>
/// The Checkers bot (Sprint 12 PR7; docs/bots.md). Pure: a move is a
/// function of (state, side, level, rng), the rng only breaks ties at the
/// root, and the search is bounded by a fixed depth — never a timer — so
/// the same inputs always pick the same move on any machine.
///
/// <para>
/// Negamax with alpha-beta over the module's own move generator
/// (<see cref="CheckersGameModule.LegalMovesFor(string?[], string, CheckersCoordinate?, bool[])"/>),
/// so the bot can never disagree with the rules about what is legal:
/// mandatory capture, the king landing rule and the Turkish strike all
/// come for free. A multi-capture chain is one decision, not several
/// plies: while the same side keeps the turn the search recurses without
/// flipping sign or spending depth. Captures are forced, which already
/// tames the horizon effect; on top of that the search never cuts off
/// at a node whose side-to-move has a capture available, extending
/// until the position is quiet (bounded, since every capture removes a
/// piece).
/// </para>
/// <list type="bullet">
///   <item><b>Easy</b> — depth 2, material only: takes what it can, keeps
///   its pieces out of an immediate capture, otherwise plays at random.</item>
///   <item><b>Medium</b> — depth 4 with the positional terms.</item>
///   <item><b>Hard</b> — iterative deepening from depth 4 to 8 with the
///   positional terms under a node budget: depth 4 always completes, each
///   deeper iteration counts against the budget and is discarded if it
///   runs out, and the deepest completed iteration's move is played.
///   Node counts are a function of the position alone, so the budget
///   bounds the work without making the move depend on machine speed.</item>
/// </list>
/// <para>
/// Evaluation: material (a king is worth three men), plus for men a
/// bonus for advancement toward promotion, for guarding the home rank,
/// and for holding the centre. Move ordering is captures of kings first,
/// then promotions, then advancing and central moves, which is what makes
/// the pruning pay. Russian draughts on 8×8 has a low branching factor,
/// so depth 8 is strong enough for a casual opponent; there is no
/// transposition table or opening book by design. The search is this
/// module's own (CLAUDE.md §7: no shared search helper).
/// </para>
/// </summary>
internal static class CheckersBot
{
    private const int WinScore = 1_000_000;
    private const int ManValue = 100;
    private const int KingValue = 300;

    /// <summary>
    /// How far past the nominal depth the capture-only extension may run.
    /// Every extended ply removes at least one piece, so the search
    /// terminates regardless; the cap just bounds a pathological tree.
    /// </summary>
    private const int QuiescenceLimit = 6;

    private const int EasyDepth = 2;
    private const int MediumDepth = 4;
    private const int HardMaxDepth = 8;

    /// <summary>
    /// Hard's node budget across its deepening iterations. Calibrated so
    /// the worst middlegame stays near the 1000 ms target on the
    /// production tier (docs/bots.md §2.2); see the probe in the PR.
    /// </summary>
    private const int HardNodeBudget = 60_000;

    private static readonly bool[] NoPending = new bool[CheckersState.CellCount];

    /// <summary>Per-call search state: the node budget and whether it ran out.</summary>
    private sealed class SearchContext
    {
        public SearchContext(bool positional, int budget)
        {
            Positional = positional;
            Budget = budget;
        }

        public bool Positional { get; }

        public int Budget { get; set; }

        public int Nodes { get; set; }

        public bool Aborted { get; set; }
    }

    public static CheckersMove Choose(CheckersState board, string side, BotLevel level, IRandomSource rng)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(rng);
        if (board.SideToMove != side)
        {
            throw new ArgumentException($"It is {board.SideToMove}'s turn, not {side}'s.", nameof(side));
        }

        var legal = board.LegalMoves;
        if (legal.Count == 0)
        {
            throw new InvalidOperationException("No legal move for the side to move.");
        }
        if (legal.Count == 1)
        {
            return ToMove(legal[0]);
        }

        var cells = CopyCells(board.Cells);
        var pending = PendingMask(board.Chain?.Captured);

        List<CheckersMoveInfo> bestMoves;
        if (level == BotLevel.Hard)
        {
            // Depth 4 is never cut short, so Hard is at least Medium; each
            // deeper iteration counts against the shared budget and, if it
            // runs out mid-way, its partial result is thrown away.
            var context = new SearchContext(positional: true, budget: int.MaxValue);
            bestMoves = RootSearch(cells, pending, side, legal, MediumDepth, context)!;
            context.Budget = HardNodeBudget;
            for (var depth = MediumDepth + 1; depth <= HardMaxDepth; depth++)
            {
                var deeper = RootSearch(cells, pending, side, legal, depth, context);
                if (deeper is null) break;
                bestMoves = deeper;
            }
        }
        else
        {
            var context = new SearchContext(positional: level != BotLevel.Easy, budget: int.MaxValue);
            bestMoves = RootSearch(
                cells, pending, side, legal, level == BotLevel.Easy ? EasyDepth : MediumDepth, context)!;
        }

        return ToMove(bestMoves[rng.NextInt(bestMoves.Count)]);
    }

    /// <summary>
    /// The root moves sharing the best score at <paramref name="depth"/>,
    /// or null if the budget ran out before the iteration completed.
    /// </summary>
    private static List<CheckersMoveInfo>? RootSearch(
        string?[] cells,
        bool[] pending,
        string side,
        IReadOnlyList<CheckersMoveInfo> legal,
        int depth,
        SearchContext context)
    {
        var best = int.MinValue;
        var bestMoves = new List<CheckersMoveInfo>();
        foreach (var move in Order(legal, cells, side))
        {
            var score = ScoreChild(cells, pending, side, move, depth, -WinScore - 1, WinScore + 1, context, ply: 0);
            if (context.Aborted) return null;
            if (score > best)
            {
                best = score;
                bestMoves.Clear();
                bestMoves.Add(move);
            }
            else if (score == best)
            {
                bestMoves.Add(move);
            }
        }
        return bestMoves;
    }

    /// <summary>
    /// Score <paramref name="move"/> for <paramref name="side"/>: apply it,
    /// then either continue the chain from the same side's perspective
    /// (same window, no depth spent) or hand the turn over (negated, one
    /// ply deeper).
    /// </summary>
    private static int ScoreChild(
        string?[] cells,
        bool[] pending,
        string side,
        CheckersMoveInfo move,
        int depth,
        int alpha,
        int beta,
        SearchContext context,
        int ply)
    {
        var next = Apply(cells, pending, side, move, out var nextPending);
        return nextPending is not null
            ? Search(next, nextPending, side, move.To, depth, alpha, beta, context, ply + 1)
            : -Search(next, NoPending, Other(side), chainAt: null, depth - 1, -beta, -alpha, context, ply + 1);
    }

    /// <summary>
    /// Negamax: the score is from <paramref name="toMove"/>'s perspective.
    /// A side with nothing to play has lost (the module's own rule). Depth
    /// is only spent on turn changes, and a position where the side to
    /// move must capture is never a leaf until <see cref="QuiescenceLimit"/>.
    /// Every node counts against the context's budget.
    /// </summary>
    private static int Search(
        string?[] cells,
        bool[] pending,
        string toMove,
        CheckersCoordinate? chainAt,
        int depth,
        int alpha,
        int beta,
        SearchContext context,
        int ply)
    {
        if (++context.Nodes > context.Budget)
        {
            // Out of budget: unwind with a dummy score; the root discards
            // this whole iteration.
            context.Aborted = true;
            return 0;
        }

        var moves = CheckersGameModule.LegalMovesFor(cells, toMove, chainAt, pending);
        if (moves.Count == 0)
        {
            // Prefer the quicker win / slower loss so a forced line is
            // followed through rather than dawdled.
            return -(WinScore - ply);
        }

        var mustCapture = moves[0].Captured is not null;
        if (depth <= 0 && (!mustCapture || depth <= -QuiescenceLimit))
        {
            return Evaluate(cells, toMove, context.Positional);
        }

        var best = int.MinValue;
        foreach (var move in moves.Count > 1 ? Order(moves, cells, toMove) : (IReadOnlyList<CheckersMoveInfo>)moves)
        {
            var score = ScoreChild(cells, pending, toMove, move, depth, alpha, beta, context, ply);
            if (context.Aborted) return 0;
            if (score > best) best = score;
            if (best > alpha) alpha = best;
            if (alpha >= beta) break;
        }
        return best;
    }

    /// <summary>
    /// Apply one step or jump on a copy of the board, mirroring the
    /// module's <c>ApplyStep</c> / <c>ApplyJump</c>: promote on landing,
    /// keep jumped pieces on the board (pending) while the chain runs, and
    /// sweep them off when it ends. <paramref name="nextPending"/> is the
    /// pending mask when the same side must continue capturing from the
    /// landing square, or null when the turn passes.
    /// </summary>
    private static string?[] Apply(
        string?[] cells, bool[] pending, string side, CheckersMoveInfo move, out bool[]? nextPending)
    {
        var next = (string?[])cells.Clone();
        var piece = next[CheckersState.IndexOf(move.From)]!;
        next[CheckersState.IndexOf(move.To)] = CheckersGameModule.Promote(piece, side, move.To.Row);
        next[CheckersState.IndexOf(move.From)] = null;

        if (move.Captured is not { } over)
        {
            nextPending = null;
            return next;
        }

        var mask = (bool[])pending.Clone();
        mask[CheckersState.IndexOf(over)] = true;
        if (CheckersGameModule.LegalMovesFor(next, side, move.To, mask).Count > 0)
        {
            nextPending = mask;
            return next;
        }

        for (var i = 0; i < CheckersState.CellCount; i++)
        {
            if (mask[i]) next[i] = null;
        }
        nextPending = null;
        return next;
    }

    /// <summary>
    /// Static score from <paramref name="me"/>'s perspective. Material
    /// always; the positional terms only when <paramref name="positional"/>.
    /// </summary>
    private static int Evaluate(string?[] cells, string me, bool positional)
    {
        var total = 0;
        for (var row = 0; row < CheckersState.Size; row++)
        {
            for (var col = 0; col < CheckersState.Size; col++)
            {
                var piece = cells[CheckersState.IndexOf(row, col)];
                var owner = CheckersPieces.SideOf(piece);
                if (owner is null) continue;

                int value;
                if (CheckersPieces.IsKing(piece))
                {
                    value = KingValue;
                }
                else
                {
                    value = ManValue;
                    if (positional)
                    {
                        value += 3 * Advancement(owner, row);
                        if (row == HomeRank(owner)) value += 8;
                        if (row is >= 2 and <= 5 && col is >= 2 and <= 5) value += 4;
                    }
                }
                total += owner == me ? value : -value;
            }
        }
        return total;
    }

    /// <summary>
    /// Captures of kings first, then promotions, then advancing and central
    /// moves. A stable insertion sort (the lists are short), so equal keys
    /// keep the generator's order and the result is deterministic.
    /// </summary>
    private static List<CheckersMoveInfo> Order(
        IReadOnlyList<CheckersMoveInfo> moves, string?[] cells, string side)
    {
        var count = moves.Count;
        var keys = new int[count];
        var sorted = new List<CheckersMoveInfo>(count);
        for (var i = 0; i < count; i++)
        {
            var move = moves[i];
            var key = OrderKey(move, cells, side);
            var at = sorted.Count;
            while (at > 0 && keys[at - 1] < key)
            {
                keys[at] = keys[at - 1];
                at--;
            }
            keys[at] = key;
            sorted.Insert(at, move);
        }
        return sorted;
    }

    private static int OrderKey(CheckersMoveInfo move, string?[] cells, string side)
    {
        var key = 0;
        if (move.Captured is { } over)
        {
            key += CheckersPieces.IsKing(cells[CheckersState.IndexOf(over)]) ? 400 : 100;
        }
        var piece = cells[CheckersState.IndexOf(move.From)];
        if (!CheckersPieces.IsKing(piece))
        {
            if (move.To.Row == FarRank(side)) key += 50;
            key += Advancement(side, move.To.Row) - Advancement(side, move.From.Row);
        }
        if (move.To.Col is >= 2 and <= 5) key += 1;
        return key;
    }

    /// <summary>Rows a man has travelled from its home rank toward promotion.</summary>
    private static int Advancement(string side, int row) =>
        side == CheckersSides.White ? CheckersState.Size - 1 - row : row;

    private static int HomeRank(string side) => side == CheckersSides.White ? CheckersState.Size - 1 : 0;

    private static int FarRank(string side) => side == CheckersSides.White ? 0 : CheckersState.Size - 1;

    private static string Other(string side) =>
        side == CheckersSides.White ? CheckersSides.Black : CheckersSides.White;

    private static CheckersMove ToMove(CheckersMoveInfo info) => new(info.From, info.To);

    private static string?[] CopyCells(IReadOnlyList<string?> source)
    {
        var copy = new string?[CheckersState.CellCount];
        for (var i = 0; i < CheckersState.CellCount; i++)
        {
            copy[i] = source[i];
        }
        return copy;
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
}
