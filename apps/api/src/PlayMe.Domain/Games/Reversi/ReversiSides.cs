namespace PlayMe.Domain.Games.Reversi;

/// <summary>
/// Side identifiers for Reversi. Lower-case per CLAUDE.md §2.3 #14. Dark
/// moves first (<see href="../../../../../docs/games/reversi.md">games/reversi.md</see>).
/// </summary>
public static class ReversiSides
{
    public const string Dark = "dark";
    public const string Light = "light";
}
