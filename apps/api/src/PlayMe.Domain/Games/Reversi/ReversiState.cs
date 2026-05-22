using PlayMe.Domain.Platform;

namespace PlayMe.Domain.Games.Reversi;

/// <summary>
/// Immutable snapshot of a Reversi board: 8×8 cells, row-major (row 0 top,
/// row 7 bottom). Carries the rendering-hint fields the Reversi web
/// renderer consumes — <see cref="LastPlacement"/>, <see cref="LastWasPass"/>,
/// <see cref="FlippedLastTurn"/>, <see cref="MustPassSide"/>, plus the
/// running disc counts. The platform never inspects any of it (CLAUDE.md
/// §7 "Platform thinness").
///
/// <para>
/// <see cref="MustPassSide"/> is set by <see cref="ReversiGameModule"/>
/// when the side-to-move on this board has no legal placement. The Reversi
/// renderer reads the flag and auto-submits a <c>{ pass: true }</c> move;
/// the platform never sees pass vocabulary.
/// </para>
/// </summary>
public sealed class ReversiState : IGameState
{
    public const int Size = 8;
    public const int CellCount = Size * Size;

    /// <summary>
    /// The number of placements after which the classic free opening (first
    /// four placements restricted to the central 2×2) ends and standard
    /// bracketing play begins. Match move count includes passes, but during
    /// the opening no passes are possible (the central 2×2 always has an
    /// empty cell while moves &lt; 4), so this constant is equivalent to a
    /// placement count.
    /// </summary>
    public const int OpeningMoveCount = 4;

    private readonly string?[] _cells;

    public IReadOnlyList<string?> Cells => _cells;
    public int MoveCount { get; }
    public ReversiCoordinate? LastPlacement { get; }
    public bool LastWasPass { get; }
    public IReadOnlyList<ReversiCoordinate> FlippedLastTurn { get; }
    public int ConsecutivePasses { get; }

    /// <summary>
    /// Side that must auto-pass on this board (no legal placements
    /// available), or <c>null</c> if both sides could move or the match has
    /// already terminated. Owned by the module — the platform is unaware.
    /// </summary>
    public string? MustPassSide { get; }

    public int DarkCount { get; }
    public int LightCount { get; }

    public ReversiState()
    {
        _cells = new string?[CellCount];
        MoveCount = 0;
        LastPlacement = null;
        LastWasPass = false;
        FlippedLastTurn = Array.Empty<ReversiCoordinate>();
        ConsecutivePasses = 0;
        MustPassSide = null;
        DarkCount = 0;
        LightCount = 0;
    }

    public ReversiState(
        IReadOnlyList<string?> cells,
        int moveCount,
        ReversiCoordinate? lastPlacement,
        bool lastWasPass,
        IReadOnlyList<ReversiCoordinate> flippedLastTurn,
        int consecutivePasses,
        string? mustPassSide)
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
        LastWasPass = lastWasPass;
        FlippedLastTurn = flippedLastTurn;
        ConsecutivePasses = consecutivePasses;
        MustPassSide = mustPassSide;
        DarkCount = dark;
        LightCount = light;
    }

    public static int IndexOf(int row, int col) => row * Size + col;

    public string? CellAt(int row, int col) => _cells[IndexOf(row, col)];

    public bool IsFull() => DarkCount + LightCount == CellCount;

    public bool InOpening => MoveCount < OpeningMoveCount;
}
