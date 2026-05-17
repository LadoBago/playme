namespace PlayMe.Domain.Games.TicTacToe9x9;

/// <summary>
/// Side identifiers for Tic-Tac-Toe 9×9. Lower-case per CLAUDE.md §2.3 #14.
/// Independent of (and intentionally not shared with) the analogous type in
/// the Tic-Tac-Toe 3×3 module — per-module duplication is acceptable
/// (CLAUDE.md §7 "Platform thinness").
/// </summary>
public static class TicTacToe9x9Sides
{
    public const string X = "x";
    public const string O = "o";
}
