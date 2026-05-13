namespace PlayMe.Domain.Platform;

/// <summary>
/// Server-authoritative chess clock snapshot for a single match
/// (state.md §2.2). Stored values represent the remaining time per
/// <em>player slot</em> (host / challenger, not the side label —
/// state.md §1: <c>hostClockMs</c>, <c>challengerClockMs</c>, <c>activePlayer</c>)
/// <em>as of</em> <see cref="LastTickAt"/>. The state is mutated only on
/// real events (move accepted, timeout adjudication, match end) — no
/// per-room periodic timer ticks it.
///
/// Keying by player slot (not side string) means a rematch with swapped
/// sides (Sprint 5) doesn't have to rewrite the clock to match — the host's
/// remaining time is the host's regardless of which side they're playing.
/// </summary>
public sealed record MatchClock(
    TimeSpan HostRemaining,
    TimeSpan ChallengerRemaining,
    Role ActivePlayer,
    DateTimeOffset LastTickAt)
{
    /// <summary>
    /// Initialize a clock with equal budgets per side; the player whose
    /// side moves first owns <see cref="ActivePlayer"/> and their clock
    /// starts ticking from <paramref name="startedAt"/>.
    /// </summary>
    public static MatchClock Start(
        TimeSpan budgetPerSide,
        Role firstMover,
        DateTimeOffset startedAt) =>
        new(budgetPerSide, budgetPerSide, firstMover, startedAt);

    /// <summary>
    /// Effective remaining time for <paramref name="role"/> at moment
    /// <paramref name="now"/>. Floors at zero — never returns negative.
    /// </summary>
    public TimeSpan EffectiveRemaining(Role role, DateTimeOffset now)
    {
        var stored = StoredFor(role);
        if (role != ActivePlayer) return stored;
        var left = stored - (now - LastTickAt);
        return left < TimeSpan.Zero ? TimeSpan.Zero : left;
    }

    /// <summary>
    /// Apply an accepted move at moment <paramref name="now"/>: subtract
    /// the active player's elapsed time from their remaining budget, flip
    /// the active player, advance <see cref="LastTickAt"/>. The flipped
    /// clock is what the opponent's clock starts ticking from.
    /// </summary>
    public MatchClock AfterMove(Role nextActive, DateTimeOffset now)
    {
        var elapsed = now - LastTickAt;
        var remaining = StoredFor(ActivePlayer) - elapsed;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;

        return ActivePlayer switch
        {
            Role.Host => this with
            {
                HostRemaining = remaining,
                ActivePlayer = nextActive,
                LastTickAt = now,
            },
            Role.Challenger => this with
            {
                ChallengerRemaining = remaining,
                ActivePlayer = nextActive,
                LastTickAt = now,
            },
            _ => throw new DomainException($"Unknown role {ActivePlayer}."),
        };
    }

    /// <summary>
    /// Apply a timeout at moment <paramref name="now"/>: zero out the
    /// active player's remaining time, advance <see cref="LastTickAt"/>.
    /// The active player is left in place so the caller can read which
    /// side timed out via <see cref="ActivePlayer"/>.
    /// </summary>
    public MatchClock AfterTimeout(DateTimeOffset now) => ActivePlayer switch
    {
        Role.Host => this with { HostRemaining = TimeSpan.Zero, LastTickAt = now },
        Role.Challenger => this with { ChallengerRemaining = TimeSpan.Zero, LastTickAt = now },
        _ => throw new DomainException($"Unknown role {ActivePlayer}."),
    };

    /// <summary>
    /// Wall-clock moment at which the active player's time will run out
    /// given the current snapshot. The sweeper schedules timeout checks
    /// at this instant; new moves invalidate the schedule by advancing
    /// <see cref="LastTickAt"/>.
    /// </summary>
    public DateTimeOffset ActivePlayerDeadline() =>
        LastTickAt + StoredFor(ActivePlayer);

    private TimeSpan StoredFor(Role role) => role switch
    {
        Role.Host => HostRemaining,
        Role.Challenger => ChallengerRemaining,
        _ => throw new DomainException($"Unknown role {role}."),
    };
}
