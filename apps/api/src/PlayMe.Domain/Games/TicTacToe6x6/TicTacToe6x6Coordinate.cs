namespace PlayMe.Domain.Games.TicTacToe6x6;

/// <summary>
/// 2D grid coordinate (zero-indexed) — used by TTT-6×6's serialized state
/// to describe the last-played cell and the winning line so the per-game
/// web renderer can highlight them. Per-game and owned by this module
/// (CLAUDE.md §7 "Platform thinness"). Independent of (and intentionally
/// not shared with) the analogous types in the 3×3, 9×9, and Connect 4
/// modules — per-module duplication is acceptable.
/// </summary>
public readonly record struct TicTacToe6x6Coordinate(int Row, int Col);
