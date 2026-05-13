namespace PlayMe.Domain.Platform;

/// <summary>
/// Terminal match result (CLAUDE.md §2.7). Sprint 1 reaches
/// <see cref="Win"/>, <see cref="Draw"/>, and <see cref="Resign"/>;
/// Sprint 2 adds <see cref="Timeout"/>. <c>Disconnect</c> arrives with the
/// abandon-grace work in Sprint 5.
/// </summary>
public abstract record Outcome;

/// <summary>One side aligned the winning pattern.</summary>
public sealed record Win(string WinningSide, IReadOnlyList<BoardCoordinate> WinningLine) : Outcome;

/// <summary>Board filled with no winner.</summary>
public sealed record Draw : Outcome;

/// <summary>A player resigned the match.</summary>
public sealed record Resign(string ResigningSide) : Outcome;

/// <summary>
/// One side's clock ran out. The opponent wins; no winning-line coordinates
/// because the board may be in any legal state.
/// </summary>
public sealed record Timeout(string TimedOutSide) : Outcome;
