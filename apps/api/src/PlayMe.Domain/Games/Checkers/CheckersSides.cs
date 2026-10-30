namespace PlayMe.Domain.Games.Checkers;

/// <summary>
/// Side identifiers for Checkers. Lower-case per CLAUDE.md §2.3 #14 and the
/// standard draughts vocabulary (distinct from Reversi's dark/light). White
/// moves first (<see href="../../../../../docs/games/checkers.md">games/checkers.md</see>).
/// </summary>
public static class CheckersSides
{
    public const string White = "white";
    public const string Black = "black";
}
