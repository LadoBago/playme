using PlayMe.Domain.Platform;

namespace PlayMe.Domain.Games.Checkers;

/// <summary>
/// Immutable snapshot of a Checkers match: 8×8 cells row-major (row 0 top =
/// black's home rank, row 7 bottom = white's), the side to move, the open
/// capture <see cref="Chain"/> if any, the server-computed
/// <see cref="LegalMoves"/> the renderer highlights from, and the draw-rule
/// bookkeeping (<see cref="KingMovePlies"/>, <see cref="History"/>). The
/// platform never inspects any of it (CLAUDE.md §7 "Platform thinness").
/// </summary>
public sealed class CheckersState : IGameState
{
    public const int Size = 8;
    public const int CellCount = Size * Size;

    /// <summary>
    /// King-only, capture-free plies after which the match is drawn:
    /// 15 moves by each side (<see cref="CheckersDrawReasons.KingMoves"/>).
    /// </summary>
    public const int KingMovePlyLimit = 30;

    /// <summary>
    /// Occurrences of one position (board + side to move) after which the
    /// match is drawn (<see cref="CheckersDrawReasons.Repetition"/>).
    /// </summary>
    public const int RepetitionLimit = 3;

    private readonly string?[] _cells;

    public IReadOnlyList<string?> Cells => _cells;

    public string SideToMove { get; }

    /// <summary>Completed turns — a multi-jump chain counts once, when it ends.</summary>
    public int MoveCount { get; }

    /// <summary>The latest accepted step or jump (mid-chain: the latest jump).</summary>
    public CheckersMoveInfo? LastMove { get; }

    /// <summary>Open capture obligation, or null between turns.</summary>
    public CheckersChain? Chain { get; }

    /// <summary>
    /// Every legal move for <see cref="SideToMove"/> given the rules
    /// (mandatory capture, chain continuation, king landing rule). Derived;
    /// the module recomputes it on <c>Deserialize</c> and never trusts a
    /// stored copy.
    /// </summary>
    public IReadOnlyList<CheckersMoveInfo> LegalMoves { get; }

    public int WhiteCount { get; }

    public int BlackCount { get; }

    /// <summary>King-only, capture-free plies since the last man move or capture.</summary>
    public int KingMovePlies { get; }

    /// <summary>
    /// Position keys (board + side to move) of every turn end since the last
    /// irreversible move — a man move or a capture clears it.
    /// </summary>
    public IReadOnlyList<string> History { get; }

    /// <summary>One of <see cref="CheckersDrawReasons"/>; set only on a drawn terminal state.</summary>
    public string? DrawReason { get; }

    public CheckersState(
        IReadOnlyList<string?> cells,
        string sideToMove,
        int moveCount,
        CheckersMoveInfo? lastMove,
        CheckersChain? chain,
        IReadOnlyList<CheckersMoveInfo> legalMoves,
        int kingMovePlies,
        IReadOnlyList<string> history,
        string? drawReason = null)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentNullException.ThrowIfNull(legalMoves);
        ArgumentNullException.ThrowIfNull(history);
        if (cells.Count != CellCount)
        {
            throw new ArgumentException($"Expected {CellCount} cells.", nameof(cells));
        }
        if (sideToMove != CheckersSides.White && sideToMove != CheckersSides.Black)
        {
            throw new ArgumentException($"Unknown side '{sideToMove}'.", nameof(sideToMove));
        }

        _cells = new string?[CellCount];
        var white = 0;
        var black = 0;
        for (var i = 0; i < CellCount; i++)
        {
            var cell = cells[i];
            if (!CheckersPieces.IsValidCell(cell))
            {
                throw new ArgumentException($"Unknown cell token '{cell}' at index {i}.", nameof(cells));
            }
            _cells[i] = cell;
            var owner = CheckersPieces.SideOf(cell);
            if (owner == CheckersSides.White) white++;
            else if (owner == CheckersSides.Black) black++;
        }

        SideToMove = sideToMove;
        MoveCount = moveCount;
        LastMove = lastMove;
        Chain = chain;
        LegalMoves = legalMoves;
        WhiteCount = white;
        BlackCount = black;
        KingMovePlies = kingMovePlies;
        History = history;
        DrawReason = drawReason;
    }

    public static int IndexOf(int row, int col) => row * Size + col;

    public static int IndexOf(CheckersCoordinate at) => IndexOf(at.Row, at.Col);

    /// <summary>Play happens on the dark squares only; a1 (row 7, col 0) is dark.</summary>
    public static bool IsDarkSquare(int row, int col) => (row + col) % 2 == 1;

    public string? CellAt(int row, int col) => _cells[IndexOf(row, col)];

    public string? CellAt(CheckersCoordinate at) => _cells[IndexOf(at)];
}
