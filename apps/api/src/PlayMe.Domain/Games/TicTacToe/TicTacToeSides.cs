namespace PlayMe.Domain.Games.TicTacToe;

/// <summary>
/// Side identifiers for the unified Tic-Tac-Toe module (Sprint 9 PR1b).
/// Lower-case per CLAUDE.md §2.3 #14. Same vocabulary as the legacy
/// per-size modules — duplication is intentional per platform-thinness:
/// each module owns its own constants rather than sharing a TTT helper.
/// </summary>
public static class TicTacToeSides
{
    public const string X = "x";
    public const string O = "o";
}
