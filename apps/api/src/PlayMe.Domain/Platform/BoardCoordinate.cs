namespace PlayMe.Domain.Platform;

/// <summary>
/// 2D grid coordinate (zero-indexed). Used by every game module to report
/// winning-line coordinates back to the client so the UI can highlight them
/// without recomputing (CLAUDE.md §2.3 cross-game UX rules).
/// </summary>
public readonly record struct BoardCoordinate(int Row, int Col);
