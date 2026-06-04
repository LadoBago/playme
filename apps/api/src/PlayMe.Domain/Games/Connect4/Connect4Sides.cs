namespace PlayMe.Domain.Games.Connect4;

/// <summary>
/// Side identifiers for Connect 4. Lower-case per CLAUDE.md §2.3 #14 and
/// the canonical Hasbro pair: red moves first
/// (<see href="../../../../../docs/games/connect4.md">games/connect4.md</see>).
/// </summary>
public static class Connect4Sides
{
    public const string Red = "red";
    public const string Yellow = "yellow";
}
