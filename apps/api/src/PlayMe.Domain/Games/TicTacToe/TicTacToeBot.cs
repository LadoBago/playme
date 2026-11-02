using PlayMe.Domain.Platform;

namespace PlayMe.Domain.Games.TicTacToe;

/// <summary>
/// The Tic-Tac-Toe bot (Sprint 12 PR5; docs/bots.md). Pure: a move is a
/// function of (state, side, level, rng), the rng only breaks ties at the
/// root, and every search is bounded by a fixed depth and a candidate
/// filter — never by a timer — so the same inputs always pick the same
/// move on any machine.
///
/// <para>
/// Every level first takes an immediate win and otherwise blocks an
/// immediate loss. Beyond that:
/// <list type="bullet">
///   <item><b>Easy</b> — a random legal cell near the action.</item>
///   <item><b>Medium</b> — alpha-beta to depth 2 over the heuristic.</item>
///   <item><b>Hard</b> — on 3×3 a full search (the game is solved and the
///   bot never loses); on 6×6 and 9×9 alpha-beta to depth 3.</item>
/// </list>
/// The heuristic sums, over every window of <see cref="TicTacToeState.WinLength"/>
/// cells, a weight that grows geometrically with the number of own marks
/// in an uncontested window, minus the same for the opponent. On the big
/// boards the search only considers empty cells within two steps of an
/// existing mark (plus the centre on an empty board), which is where every
/// relevant move lives and what keeps a 9×9 depth-3 search well inside the
/// one-second target on the production tier.
/// </para>
/// </summary>
internal static class TicTacToeBot
{
    private const int WinScore = 1_000_000;
    private const int FullSearchDepth = 9;
    private const int NeighbourhoodRadius = 2;

    /// <summary>Weight for an uncontested window holding n own marks.</summary>
    private static readonly int[] WindowWeights = { 0, 1, 10, 100, 1_000, 10_000 };

    private static readonly Dictionary<int, int[][]> WindowsBySize = new();

    public static TicTacToeMove Choose(TicTacToeState board, string side, BotLevel level, IRandomSource rng)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(rng);
        var opponent = side == TicTacToeSides.X ? TicTacToeSides.O : TicTacToeSides.X;
        var candidates = Candidates(board);
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("No legal move: the board is full.");
        }

        var winning = FirstCompleting(board, candidates, side);
        if (winning is not null) return new TicTacToeMove(winning.Value);

        var block = FirstCompleting(board, candidates, opponent);
        if (block is not null) return new TicTacToeMove(block.Value);

        if (level == BotLevel.Easy)
        {
            return new TicTacToeMove(candidates[rng.NextInt(candidates.Count)]);
        }

        var depth = DepthFor(board.BoardSize, level);
        var cells = (string?[])board.Cells.ToArray();
        var windows = WindowsFor(board.BoardSize, board.WinLength);
        var best = int.MinValue;
        var bestCells = new List<int>();
        foreach (var cell in candidates)
        {
            cells[cell] = side;
            var score = CompletesRun(board, cells, cell, side)
                ? WinScore
                : -Negamax(board, cells, windows, opponent, side, depth - 1, -WinScore - 1, WinScore + 1);
            cells[cell] = null;

            if (score > best)
            {
                best = score;
                bestCells.Clear();
                bestCells.Add(cell);
            }
            else if (score == best)
            {
                bestCells.Add(cell);
            }
        }

        return new TicTacToeMove(bestCells[rng.NextInt(bestCells.Count)]);
    }

    private static int DepthFor(int boardSize, BotLevel level) => (boardSize, level) switch
    {
        (3, BotLevel.Hard) => FullSearchDepth,
        (_, BotLevel.Hard) => 3,
        _ => 2,
    };

    /// <summary>
    /// Negamax with alpha-beta. <paramref name="toMove"/> is about to play;
    /// the score is from <paramref name="toMove"/>'s perspective.
    /// </summary>
    private static int Negamax(
        TicTacToeState board, string?[] cells, int[][] windows,
        string toMove, string other, int depth, int alpha, int beta)
    {
        var candidates = Candidates(board, cells);
        if (candidates.Count == 0) return 0;
        if (depth == 0) return Evaluate(cells, windows, toMove, other);

        var best = int.MinValue;
        foreach (var cell in candidates)
        {
            cells[cell] = toMove;
            // Prefer the quicker win / slower loss so a forced line is
            // followed through instead of dawdled.
            var score = CompletesRun(board, cells, cell, toMove)
                ? WinScore - (FullSearchDepth - depth)
                : -Negamax(board, cells, windows, other, toMove, depth - 1, -beta, -alpha);
            cells[cell] = null;

            if (score > best) best = score;
            if (best > alpha) alpha = best;
            if (alpha >= beta) break;
        }
        return best;
    }

    private static int Evaluate(string?[] cells, int[][] windows, string me, string other)
    {
        var total = 0;
        foreach (var window in windows)
        {
            var mine = 0;
            var theirs = 0;
            foreach (var cell in window)
            {
                var mark = cells[cell];
                if (mark is null) continue;
                if (mark == me) mine++;
                else theirs++;
            }
            if (mine > 0 && theirs > 0) continue;
            if (mine > 0) total += WindowWeights[mine];
            else if (theirs > 0) total -= WindowWeights[theirs];
        }
        return total;
    }

    private static int? FirstCompleting(TicTacToeState board, List<int> candidates, string side)
    {
        var cells = (string?[])board.Cells.ToArray();
        foreach (var cell in candidates)
        {
            cells[cell] = side;
            var completes = CompletesRun(board, cells, cell, side);
            cells[cell] = null;
            if (completes) return cell;
        }
        return null;
    }

    /// <summary>
    /// Does the mark just placed at <paramref name="cell"/> (already written
    /// into <paramref name="cells"/>) complete a run of at least the win
    /// length? Same walk as the module's win probe, over the scratch array.
    /// </summary>
    private static bool CompletesRun(TicTacToeState board, string?[] cells, int cell, string side)
    {
        var size = board.BoardSize;
        var row = cell / size;
        var col = cell % size;
        Span<(int, int)> directions = stackalloc (int, int)[] { (0, 1), (1, 0), (1, 1), (-1, 1) };
        foreach (var (dr, dc) in directions)
        {
            var run = 1;
            for (var step = 1; ; step++)
            {
                var r = row + dr * step;
                var c = col + dc * step;
                if (r < 0 || r >= size || c < 0 || c >= size || cells[r * size + c] != side) break;
                run++;
            }
            for (var step = 1; ; step++)
            {
                var r = row - dr * step;
                var c = col - dc * step;
                if (r < 0 || r >= size || c < 0 || c >= size || cells[r * size + c] != side) break;
                run++;
            }
            if (run >= board.WinLength) return true;
        }
        return false;
    }

    private static List<int> Candidates(TicTacToeState board) =>
        Candidates(board, (string?[])board.Cells.ToArray());

    /// <summary>
    /// Empty cells worth considering. On 3×3 that is every empty cell. On
    /// the big boards it is every empty cell within
    /// <see cref="NeighbourhoodRadius"/> of a mark — or the centre when the
    /// board is empty — in ascending index order so the search is
    /// deterministic.
    /// </summary>
    private static List<int> Candidates(TicTacToeState board, string?[] cells)
    {
        var size = board.BoardSize;
        var result = new List<int>();
        if (size == 3)
        {
            for (var i = 0; i < cells.Length; i++)
            {
                if (cells[i] is null) result.Add(i);
            }
            return result;
        }

        var any = false;
        var near = new bool[cells.Length];
        for (var i = 0; i < cells.Length; i++)
        {
            if (cells[i] is null) continue;
            any = true;
            var row = i / size;
            var col = i % size;
            for (var dr = -NeighbourhoodRadius; dr <= NeighbourhoodRadius; dr++)
            {
                for (var dc = -NeighbourhoodRadius; dc <= NeighbourhoodRadius; dc++)
                {
                    var r = row + dr;
                    var c = col + dc;
                    if (r < 0 || r >= size || c < 0 || c >= size) continue;
                    near[r * size + c] = true;
                }
            }
        }

        if (!any)
        {
            result.Add((size / 2) * size + size / 2);
            return result;
        }

        for (var i = 0; i < cells.Length; i++)
        {
            if (cells[i] is null && near[i]) result.Add(i);
        }
        return result;
    }

    private static int[][] WindowsFor(int size, int winLength)
    {
        lock (WindowsBySize)
        {
            if (WindowsBySize.TryGetValue(size, out var cached)) return cached;

            var windows = new List<int[]>();
            Span<(int, int)> directions = stackalloc (int, int)[] { (0, 1), (1, 0), (1, 1), (-1, 1) };
            for (var row = 0; row < size; row++)
            {
                for (var col = 0; col < size; col++)
                {
                    foreach (var (dr, dc) in directions)
                    {
                        var endRow = row + dr * (winLength - 1);
                        var endCol = col + dc * (winLength - 1);
                        if (endRow < 0 || endRow >= size || endCol < 0 || endCol >= size) continue;
                        var window = new int[winLength];
                        for (var k = 0; k < winLength; k++)
                        {
                            window[k] = (row + dr * k) * size + (col + dc * k);
                        }
                        windows.Add(window);
                    }
                }
            }

            var built = windows.ToArray();
            WindowsBySize[size] = built;
            return built;
        }
    }
}
