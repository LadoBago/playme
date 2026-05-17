namespace PlayMe.Domain.Games.TicTacToe9x9;

/// <summary>
/// 2D grid coordinate (zero-indexed) — used by TTT-9×9's serialized state to
/// describe the winning line so the per-game web renderer can highlight it.
/// Per-game and owned by this module; the platform layer doesn't know grid
/// coordinates exist (CLAUDE.md §7 "Platform thinness"). Independent of (and
/// intentionally not shared with) the analogous type in the Tic-Tac-Toe 3×3
/// module — per-module duplication is acceptable.
/// </summary>
public readonly record struct TicTacToe9x9Coordinate(int Row, int Col);
