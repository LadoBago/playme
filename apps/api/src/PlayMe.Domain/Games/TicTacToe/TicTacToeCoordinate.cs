namespace PlayMe.Domain.Games.TicTacToe;

/// <summary>
/// 2D grid coordinate (zero-indexed) on the host-chosen N×N board. Used
/// by the unified TTT state's serialized shape to describe the winning
/// line so the per-game web renderer can highlight it. Per-game and
/// owned by this module; the platform layer doesn't know grid coordinates
/// exist (CLAUDE.md §7 "Platform thinness").
/// </summary>
public readonly record struct TicTacToeCoordinate(int Row, int Col);
