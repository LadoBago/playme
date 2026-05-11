namespace PlayMe.Domain.Platform;

/// <summary>
/// Terminal match result (CLAUDE.md §2.7). Sprint 1 reaches
/// <see cref="Win"/>, <see cref="Draw"/>, and <see cref="Resign"/>;
/// <c>Timeout</c> and <c>Disconnect</c> arrive with the clock (Sprint 2)
/// and abandon-grace (Sprint 5) work.
/// </summary>
public abstract record Outcome;

/// <summary>One side aligned the winning pattern.</summary>
public sealed record Win(string WinningSide, IReadOnlyList<BoardCoordinate> WinningLine) : Outcome;

/// <summary>Board filled with no winner.</summary>
public sealed record Draw : Outcome;

/// <summary>A player resigned the match.</summary>
public sealed record Resign(string ResigningSide) : Outcome;
