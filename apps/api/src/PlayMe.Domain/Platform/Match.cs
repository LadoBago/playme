namespace PlayMe.Domain.Platform;

/// <summary>
/// One round of gameplay inside a <see cref="Room"/> (CLAUDE.md §2.7). Holds
/// the opaque per-game <see cref="State"/>, the side whose turn it is, the
/// <see cref="Clock"/> snapshot, and the <see cref="Outcome"/> once the
/// match terminates. Resigns are recorded directly via <see cref="Resign"/>;
/// move-driven endings flow through <see cref="ApplyAcceptedMove"/> after
/// the module judges the move; clock-driven endings flow through
/// <see cref="ApplyTimeout"/>.
/// </summary>
public sealed class Match
{
    public GameId GameId { get; }
    public IGameState State { get; private set; }
    public string SideToMove { get; private set; }
    public int MoveCount { get; private set; }
    public MatchClock Clock { get; private set; }
    public Outcome? Outcome { get; private set; }

    /// <summary>Platform-side setup bookkeeping (Sprint 10 seam C): which
    /// roles have committed their one-and-final setup payload. Always false
    /// for setup-less games. Completion itself is the module's call
    /// (<see cref="ISetupGame.IsSetupComplete"/>) — these flags exist for
    /// double-commit rejection and deadline adjudication.</summary>
    public bool HostSetupCommitted { get; private set; }

    /// <inheritdoc cref="HostSetupCommitted"/>
    public bool ChallengerSetupCommitted { get; private set; }

    public bool IsEnded => Outcome is not null;

    private Match(
        GameId gameId,
        IGameState state,
        string sideToMove,
        int moveCount,
        MatchClock clock,
        Outcome? outcome,
        bool hostSetupCommitted,
        bool challengerSetupCommitted)
    {
        GameId = gameId;
        State = state;
        SideToMove = sideToMove;
        MoveCount = moveCount;
        Clock = clock;
        Outcome = outcome;
        HostSetupCommitted = hostSetupCommitted;
        ChallengerSetupCommitted = challengerSetupCommitted;
    }

    public static Match Start(
        GameId gameId,
        IGameState initialState,
        string firstMoveSide,
        Role firstMover,
        TimeSpan clockBudget,
        DateTimeOffset startedAt) =>
        new(
            gameId,
            initialState,
            firstMoveSide,
            moveCount: 0,
            clock: MatchClock.Start(clockBudget, firstMover, startedAt),
            outcome: null,
            hostSetupCommitted: false,
            challengerSetupCommitted: false);

    /// <summary>
    /// Rehydrate a match snapshot from persistence. Used by the Infrastructure
    /// layer; application code constructs matches via <see cref="Start"/>.
    /// </summary>
    public static Match Rehydrate(
        GameId gameId,
        IGameState state,
        string sideToMove,
        int moveCount,
        MatchClock clock,
        Outcome? outcome,
        bool hostSetupCommitted = false,
        bool challengerSetupCommitted = false) =>
        new(gameId, state, sideToMove, moveCount, clock, outcome,
            hostSetupCommitted, challengerSetupCommitted);

    /// <summary>
    /// Commit one role's setup payload (Sprint 10 seam C): replace the
    /// state with the module's post-<see cref="ISetupGame.ApplySetup"/>
    /// result and record the commitment. No clock, turn, or move-count
    /// effect — setup actions are not moves.
    /// </summary>
    public void ApplySetup(IGameState newState, Role committer)
    {
        if (IsEnded)
        {
            throw new DomainException("Cannot apply a setup to a finished match.");
        }
        if (HasCommittedSetup(committer))
        {
            throw new DomainException($"{committer} has already committed their setup.");
        }

        State = newState;
        switch (committer)
        {
            case Role.Host: HostSetupCommitted = true; break;
            case Role.Challenger: ChallengerSetupCommitted = true; break;
        }
    }

    public bool HasCommittedSetup(Role role) => role switch
    {
        Role.Host => HostSetupCommitted,
        Role.Challenger => ChallengerSetupCommitted,
        _ => false,
    };

    /// <summary>
    /// Start the chess clock when the setup phase completes
    /// (docs/platform.md §1 #12: the clock starts when the match enters
    /// InProgress). The clock was created at SettingUp entry with
    /// <c>LastTickAt</c> = the entry moment; re-stamping it to
    /// <paramref name="now"/> discards the unclocked setup time so the
    /// first mover starts from the full budget.
    /// </summary>
    public void BeginPlayAfterSetup(DateTimeOffset now)
    {
        if (IsEnded)
        {
            throw new DomainException("Cannot start play on a finished match.");
        }
        Clock = Clock with { LastTickAt = now };
    }

    /// <summary>
    /// Commit an accepted move's effect: swap the side to move, increment
    /// the counter, advance the clock (decrement the moving side's time by
    /// elapsed, flip the active player), and record the ending if the move
    /// terminated the match.
    /// </summary>
    public void ApplyAcceptedMove(
        IGameState newState,
        string nextSideToMove,
        Role nextActivePlayer,
        DateTimeOffset now,
        Outcome? ending)
    {
        if (IsEnded)
        {
            throw new DomainException("Cannot apply a move to a finished match.");
        }

        State = newState;
        SideToMove = nextSideToMove;
        MoveCount++;
        Clock = Clock.AfterMove(nextActivePlayer, now);
        Outcome = ending;
    }

    /// <summary>
    /// End the match because the active player's clock ran out. The
    /// caller should have verified via <see cref="MatchClock.EffectiveRemaining"/>
    /// that the player's time is genuinely ≤ 0 at <paramref name="now"/>.
    /// </summary>
    public void ApplyTimeout(string timedOutSide, DateTimeOffset now)
    {
        if (IsEnded)
        {
            throw new DomainException("Cannot apply a timeout to a finished match.");
        }

        Clock = Clock.AfterTimeout(now);
        Outcome = new Timeout(timedOutSide);
    }

    public void Resign(string resigningSide)
    {
        if (IsEnded)
        {
            throw new DomainException("Cannot resign a finished match.");
        }

        Outcome = new Resign(resigningSide);
    }

    /// <summary>
    /// End the match because the reconnect-grace hard cutoff elapsed
    /// (docs/platform.md §1 #7). Records which side lost so the
    /// outcome reads correctly for both clients.
    /// </summary>
    public void ApplyDisconnect(string losingSide, DateTimeOffset now)
    {
        if (IsEnded)
        {
            throw new DomainException("Cannot apply a disconnect to a finished match.");
        }

        // Mirror ApplyTimeout in advancing the clock to `now` so the
        // frozen snapshot rendered post-match shows the abandon moment,
        // not an earlier last-move timestamp.
        Clock = Clock.AfterTimeout(now);
        Outcome = new Disconnect(losingSide);
    }
}
