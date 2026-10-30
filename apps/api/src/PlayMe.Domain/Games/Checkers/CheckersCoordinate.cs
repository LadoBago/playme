namespace PlayMe.Domain.Games.Checkers;

/// <summary>
/// 2D board coordinate (zero-indexed, row-major; row 0 is black's home
/// rank at the top of white's view). Per-game and owned by this module
/// (CLAUDE.md §7 "Platform thinness") — intentionally not shared with the
/// analogous types in other game modules; per-module duplication is
/// acceptable.
/// </summary>
public readonly record struct CheckersCoordinate(int Row, int Col);
