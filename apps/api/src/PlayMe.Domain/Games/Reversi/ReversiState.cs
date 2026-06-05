using PlayMe.Domain.Platform;

namespace PlayMe.Domain.Games.Reversi;

/// <summary>
/// Immutable snapshot of a Reversi board: 8×8 cells, row-major (row 0 top,
/// row 7 bottom). Carries the rendering-hint fields the Reversi web
/// renderer consumes — <see cref="LastPlacement"/>,
/// <see cref="FlippedLastTurn"/>, <see cref="SkippedSide"/>, plus the
/// running disc counts. The platform never inspects any of it
/// (CLAUDE.md §7 "Platform thinness").
///
/// <para>
/// <see cref="SkippedSide"/> names the side whose turn was skipped because
/// the placement producing this state left them without a legal move; the
/// mover retained the turn via <see cref="Platform.MoveResult.KeepTurn"/>
/// (seam B). The renderer uses it to pick a per-side toast — the skipped
/// player sees "you have no move", the mover sees "opponent has no move".
/// Null on any state whose placement did not strand the opponent.
/// </para>
/// </summary>
public sealed class ReversiState : IGameState
{
    public const int Size = 8;
    public const int CellCount = Size * Size;

    /// <summary>
    /// The number of placements after which the classic free opening (first
    /// four placements restricted to the central 2×2) ends and standard
    /// bracketing play begins. Forced skips are impossible during the
    /// opening (the central 2×2 always has an empty cell while moves
    /// &lt; 4), so every counted move is a placement.
    /// </summary>
    public const int OpeningMoveCount = 4;

    private readonly string?[] _cells;

    public IReadOnlyList<string?> Cells => _cells;
    public int MoveCount { get; }
    public ReversiCoordinate? LastPlacement { get; }
    public IReadOnlyList<ReversiCoordinate> FlippedLastTurn { get; }

    /// <summary>
    /// Side whose turn was skipped by the placement that produced this
    /// state (they had no legal move; the mover kept the turn). Lets the
    /// renderer pick a per-side toast — skipped player vs. mover. Null on
    /// any state whose placement did not strand the opponent. Owned by the
    /// module — the platform is unaware.
    /// </summary>
    public string? SkippedSide { get; }

    public int DarkCount { get; }
    public int LightCount { get; }

    public ReversiState()
    {
        _cells = new string?[CellCount];
        MoveCount = 0;
        LastPlacement = null;
        FlippedLastTurn = Array.Empty<ReversiCoordinate>();
        SkippedSide = null;
        DarkCount = 0;
        LightCount = 0;
    }

    public ReversiState(
        IReadOnlyList<string?> cells,
        int moveCount,
        ReversiCoordinate? lastPlacement,
        IReadOnlyList<ReversiCoordinate> flippedLastTurn,
        string? skippedSide = null)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentNullException.ThrowIfNull(flippedLastTurn);
        if (cells.Count != CellCount)
        {
            throw new ArgumentException($"Expected {CellCount} cells.", nameof(cells));
        }

        _cells = new string?[CellCount];
        var dark = 0;
        var light = 0;
        for (var i = 0; i < CellCount; i++)
        {
            var cell = cells[i];
            _cells[i] = cell;
            if (cell == ReversiSides.Dark) dark++;
            else if (cell == ReversiSides.Light) light++;
        }

        MoveCount = moveCount;
        LastPlacement = lastPlacement;
        FlippedLastTurn = flippedLastTurn;
        SkippedSide = skippedSide;
        DarkCount = dark;
        LightCount = light;
    }

    public static int IndexOf(int row, int col) => row * Size + col;

    public string? CellAt(int row, int col) => _cells[IndexOf(row, col)];

    public bool IsFull() => DarkCount + LightCount == CellCount;

    public bool InOpening => MoveCount < OpeningMoveCount;
}
