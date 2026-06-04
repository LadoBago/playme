namespace PlayMe.Domain.Games.SeaBattle;

/// <summary>
/// Side identifiers for Sea Battle. Lower-case per CLAUDE.md §2.3 #14. The
/// side determines only who shoots first — there is no other asymmetry
/// (<see href="../../../../../docs/games/seabattle.md">games/seabattle.md</see>).
/// </summary>
public static class SeaBattleSides
{
    public const string First = "first";
    public const string Second = "second";
}
