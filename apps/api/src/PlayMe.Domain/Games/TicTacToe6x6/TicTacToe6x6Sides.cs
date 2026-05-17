namespace PlayMe.Domain.Games.TicTacToe6x6;

/// <summary>
/// Side identifiers for Tic-Tac-Toe 6×6. Lower-case per CLAUDE.md §2.3 #14.
/// Per-module vocabulary — intentionally not shared with the 3×3 or 9×9
/// modules even though every TTT variant uses the same X/O alphabet
/// (CLAUDE.md §7 "Platform thinness": per-module duplication is acceptable;
/// don't extract a shared TicTacToeSides type).
/// </summary>
public static class TicTacToe6x6Sides
{
    public const string X = "x";
    public const string O = "o";
}
