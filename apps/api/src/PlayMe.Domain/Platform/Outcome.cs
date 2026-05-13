namespace PlayMe.Domain.Platform;

/// <summary>
/// Terminal match result. The platform records the <em>that</em> of a win
/// (which side won) — the <em>how</em> (winning-line cells, mating piece,
/// etc.) belongs to each game's own state, exposed by the module's
/// serializer (CLAUDE.md §7 "Platform thinness"). Sprint 1 reaches
/// <see cref="Win"/>, <see cref="Draw"/>, and <see cref="Resign"/>;
/// Sprint 2 adds <see cref="Timeout"/>. <c>Disconnect</c> arrives with the
/// abandon-grace work in Sprint 5.
/// </summary>
public abstract record Outcome;

/// <summary>One side won.</summary>
public sealed record Win(string WinningSide) : Outcome;

/// <summary>Board filled with no winner.</summary>
public sealed record Draw : Outcome;

/// <summary>A player resigned the match.</summary>
public sealed record Resign(string ResigningSide) : Outcome;

/// <summary>
/// One side's clock ran out. The opponent wins.
/// </summary>
public sealed record Timeout(string TimedOutSide) : Outcome;
