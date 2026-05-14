namespace PlayMe.Domain.Games.Connect4;

/// <summary>
/// 2D grid coordinate (zero-indexed) — used by the Connect 4 serialized
/// state to describe the last-played disc position and the winning line so
/// the per-game web renderer can highlight them. Per-game and owned by
/// this module (CLAUDE.md §7 "Platform thinness"). Independent of (and
/// intentionally not shared with) the analogous type in the Tic-Tac-Toe
/// module — per-module duplication is acceptable.
/// </summary>
public readonly record struct Connect4Coordinate(int Row, int Col);
