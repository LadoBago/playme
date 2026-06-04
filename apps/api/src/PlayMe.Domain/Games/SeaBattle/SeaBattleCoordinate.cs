namespace PlayMe.Domain.Games.SeaBattle;

/// <summary>
/// 2D grid coordinate (zero-indexed, x = column, y = row) on a Sea Battle
/// 10×10 grid. Per-game and owned by this module (CLAUDE.md §7 "Platform
/// thinness") — intentionally not shared with the analogous types in other
/// game modules; per-module duplication is acceptable.
/// </summary>
public readonly record struct SeaBattleCoordinate(int X, int Y);
