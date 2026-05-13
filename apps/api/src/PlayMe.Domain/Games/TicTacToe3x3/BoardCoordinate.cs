namespace PlayMe.Domain.Games.TicTacToe3x3;

/// <summary>
/// 2D grid coordinate (zero-indexed) — used by TTT's serialized state to
/// describe the winning line so the per-game web renderer can highlight it.
/// Per-game and owned by this module; the platform layer doesn't know grid
/// coordinates exist (CLAUDE.md §7 "Platform thinness").
/// </summary>
public readonly record struct BoardCoordinate(int Row, int Col);
