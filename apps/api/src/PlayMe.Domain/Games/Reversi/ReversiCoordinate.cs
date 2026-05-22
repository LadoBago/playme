namespace PlayMe.Domain.Games.Reversi;

/// <summary>
/// 2D grid coordinate (zero-indexed) — used by the Reversi serialized state
/// to describe the last placement and the discs flipped by it so the
/// per-game web renderer can highlight them. Per-game and owned by this
/// module (CLAUDE.md §7 "Platform thinness"). Independent of (and
/// intentionally not shared with) the analogous types in other game
/// modules — per-module duplication is acceptable.
/// </summary>
public readonly record struct ReversiCoordinate(int Row, int Col);
