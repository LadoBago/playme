using PlayMe.Domain.Platform;

namespace PlayMe.Domain.Games.Connect4;

/// <summary>
/// The Connect 4 bot (Sprint 12 PR6; docs/bots.md). Pure: a move is a
/// function of (state, side, level, rng), the rng only breaks ties at the
/// root, and the search is bounded by a fixed depth — never a timer — so
/// the same inputs always pick the same column on any machine.
///
/// <para>
/// Every level first takes an immediate win and otherwise blocks an
/// immediate loss. Beyond that:
/// <list type="bullet">
///   <item><b>Easy</b> — a random legal column.</item>
///   <item><b>Medium</b> — alpha-beta to depth 4.</item>
///   <item><b>Hard</b> — alpha-beta to depth 8.</item>
/// </list>
/// Columns are searched centre-first, which is both the strongest
/// ordering for pruning in this game and a sound tie-break. The heuristic
/// sums, over every window of four cells, a weight that grows with the
/// number of own discs in an uncontested window, minus the same for the
/// opponent. With at most seven replies per ply the whole tree is small;
/// the search is its own duplicate of the window idea used by other
/// modules by design (CLAUDE.md §7: no shared search helper).
/// </para>
/// </summary>
internal static class Connect4Bot
{
    private const int WinScore = 1_000_000;
    private const int RunLength = 4;

    /// <summary>Centre-first column order: best pruning, deterministic ties.</summary>
    private static readonly int[] ColumnOrder = { 3, 2, 4, 1, 5, 0, 6 };

    /// <summary>
    /// Weight for an uncontested window holding n own discs. The search
    /// never evaluates a position with a completed four (the drop that
    /// completes it is scored as a win first), but a caller could hand in
    /// an already-won board; the fifth entry keeps that from indexing out
    /// of range.
    /// </summary>
    private static readonly int[] WindowWeights = { 0, 1, 10, 100, 1_000 };

    private static readonly int[][] Windows = BuildWindows();

    public static Connect4Move Choose(Connect4State board, string side, BotLevel level, IRandomSource rng)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(rng);
        var opponent = side == Connect4Sides.Red ? Connect4Sides.Yellow : Connect4Sides.Red;
        var cells = (string?[])board.Cells.ToArray();
        var legal = LegalColumns(cells);
        if (legal.Count == 0)
        {
            throw new InvalidOperationException("No legal move: the board is full.");
        }

        var winning = FirstCompleting(cells, legal, side);
        if (winning is not null) return new Connect4Move(winning.Value);

        var block = FirstCompleting(cells, legal, opponent);
        if (block is not null) return new Connect4Move(block.Value);

        if (level == BotLevel.Easy)
        {
            return new Connect4Move(legal[rng.NextInt(legal.Count)]);
        }

        var depth = level == BotLevel.Hard ? 8 : 4;
        var best = int.MinValue;
        var bestColumns = new List<int>();
        foreach (var col in legal)
        {
            var row = Drop(cells, col, side);
            var score = CompletesRun(cells, row, col, side)
                ? WinScore
                : -Negamax(cells, opponent, side, depth - 1, -WinScore - 1, WinScore + 1);
            cells[Connect4State.IndexOf(row, col)] = null;

            if (score > best)
            {
                best = score;
                bestColumns.Clear();
                bestColumns.Add(col);
            }
            else if (score == best)
            {
                bestColumns.Add(col);
            }
        }

        return new Connect4Move(bestColumns[rng.NextInt(bestColumns.Count)]);
    }

    /// <summary>
    /// Negamax with alpha-beta. <paramref name="toMove"/> is about to play;
    /// the score is from <paramref name="toMove"/>'s perspective.
    /// </summary>
    private static int Negamax(string?[] cells, string toMove, string other, int depth, int alpha, int beta)
    {
        var legal = LegalColumns(cells);
        if (legal.Count == 0) return 0;
        if (depth == 0) return Evaluate(cells, toMove, other);

        var best = int.MinValue;
        foreach (var col in legal)
        {
            var row = Drop(cells, col, toMove);
            // Prefer the quicker win / slower loss so a forced line is
            // followed through rather than dawdled.
            var score = CompletesRun(cells, row, col, toMove)
                ? WinScore - (16 - depth)
                : -Negamax(cells, other, toMove, depth - 1, -beta, -alpha);
            cells[Connect4State.IndexOf(row, col)] = null;

            if (score > best) best = score;
            if (best > alpha) alpha = best;
            if (alpha >= beta) break;
        }
        return best;
    }

    private static int Evaluate(string?[] cells, string me, string other)
    {
        var total = 0;
        foreach (var window in Windows)
        {
            var mine = 0;
            var theirs = 0;
            foreach (var cell in window)
            {
                var disc = cells[cell];
                if (disc is null) continue;
                if (disc == me) mine++;
                else theirs++;
            }
            if (mine > 0 && theirs > 0) continue;
            if (mine > 0) total += WindowWeights[mine];
            else if (theirs > 0) total -= WindowWeights[theirs];
        }
        return total;
    }

    private static int? FirstCompleting(string?[] cells, List<int> legal, string side)
    {
        foreach (var col in legal)
        {
            var row = Drop(cells, col, side);
            var completes = CompletesRun(cells, row, col, side);
            cells[Connect4State.IndexOf(row, col)] = null;
            if (completes) return col;
        }
        return null;
    }

    /// <summary>Place a disc by gravity and return its row. Caller must know the column is legal.</summary>
    private static int Drop(string?[] cells, int col, string side)
    {
        for (var row = Connect4State.Rows - 1; row >= 0; row--)
        {
            var index = Connect4State.IndexOf(row, col);
            if (cells[index] is null)
            {
                cells[index] = side;
                return row;
            }
        }
        throw new InvalidOperationException($"Column {col} is full.");
    }

    /// <summary>
    /// Does the disc just placed at (<paramref name="row"/>, <paramref name="col"/>)
    /// (already written into <paramref name="cells"/>) complete four in a
    /// row? Same walk as the module's win probe, over the scratch array.
    /// </summary>
    private static bool CompletesRun(string?[] cells, int row, int col, string side)
    {
        Span<(int, int)> directions = stackalloc (int, int)[] { (0, 1), (1, 0), (1, 1), (-1, 1) };
        foreach (var (dr, dc) in directions)
        {
            var run = 1;
            for (var step = 1; ; step++)
            {
                var r = row + dr * step;
                var c = col + dc * step;
                if (r < 0 || r >= Connect4State.Rows || c < 0 || c >= Connect4State.Cols
                    || cells[Connect4State.IndexOf(r, c)] != side) break;
                run++;
            }
            for (var step = 1; ; step++)
            {
                var r = row - dr * step;
                var c = col - dc * step;
                if (r < 0 || r >= Connect4State.Rows || c < 0 || c >= Connect4State.Cols
                    || cells[Connect4State.IndexOf(r, c)] != side) break;
                run++;
            }
            if (run >= RunLength) return true;
        }
        return false;
    }

    /// <summary>Columns with room, in <see cref="ColumnOrder"/>.</summary>
    private static List<int> LegalColumns(string?[] cells)
    {
        var result = new List<int>(Connect4State.Cols);
        foreach (var col in ColumnOrder)
        {
            if (cells[Connect4State.IndexOf(0, col)] is null) result.Add(col);
        }
        return result;
    }

    private static int[][] BuildWindows()
    {
        var windows = new List<int[]>();
        var directions = new[] { (0, 1), (1, 0), (1, 1), (-1, 1) };
        for (var row = 0; row < Connect4State.Rows; row++)
        {
            for (var col = 0; col < Connect4State.Cols; col++)
            {
                foreach (var (dr, dc) in directions)
                {
                    var endRow = row + dr * (RunLength - 1);
                    var endCol = col + dc * (RunLength - 1);
                    if (endRow < 0 || endRow >= Connect4State.Rows || endCol < 0 || endCol >= Connect4State.Cols) continue;
                    var window = new int[RunLength];
                    for (var k = 0; k < RunLength; k++)
                    {
                        window[k] = Connect4State.IndexOf(row + dr * k, col + dc * k);
                    }
                    windows.Add(window);
                }
            }
        }
        return windows.ToArray();
    }
}
