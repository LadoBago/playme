using System.Text.Json;

namespace PlayMe.Domain.Platform;

/// <summary>
/// Container for one matchmaking session (CLAUDE.md §2.7, §2.9). A room
/// holds at most two players, the configured game and side-selection mode,
/// the current match, and presence flags so the platform can decide when to
/// transition <see cref="RoomStatus.WaitingForOpponent"/> → <see cref="RoomStatus.InProgress"/>.
///
/// Sprint 1 implements the WaitingForOpponent → InProgress → Ended path
/// only; rematch and post-Ended cleanup transitions are deferred to later
/// sprints (CLAUDE.md §11).
/// </summary>
public sealed class Room
{
    public RoomCode Code { get; }
    public GameId GameId { get; }

    /// <summary>
    /// Per-room game-specific options blob, set at room creation and
    /// immutable for the room's lifetime (Sprint 9 PR1). Opaque to the
    /// platform — only the game module deserializes / validates it
    /// (CLAUDE.md §7 "Platform thinness"). Null for games that don't
    /// take options.
    /// </summary>
    public JsonElement? GameOptions { get; }

    public SideSelectionMode SideSelectionMode { get; }
    public DateTimeOffset CreatedAt { get; }

    public Player Host { get; private set; }
    public Player? Challenger { get; private set; }

    public RoomStatus Status { get; private set; }
    public Match? CurrentMatch { get; private set; }

    public bool HostConnected { get; private set; }
    public bool ChallengerConnected { get; private set; }

    /// <summary>Session-only series scoreboard. Survives across rematches
    /// in the same room (docs/platform.md §1 #13). Resets only
    /// when the room reaches <see cref="RoomStatus.Closed"/> /
    /// <see cref="RoomStatus.Expired"/> (i.e. when the room itself dies).</summary>
    public SeriesScore SeriesScore { get; private set; }

    /// <summary>The role that offered the current rematch — set when the
    /// room enters <see cref="RoomStatus.AwaitingRematch"/>, cleared on
    /// transition out. The opposite role is the responder authorised to
    /// accept or reject (docs/platform.md §1 #10).</summary>
    public Role? RematchOffererRole { get; private set; }

    private Room(
        RoomCode code,
        GameId gameId,
        SideSelectionMode sideSelectionMode,
        DateTimeOffset createdAt,
        Player host,
        Player? challenger,
        RoomStatus status,
        Match? currentMatch,
        bool hostConnected,
        bool challengerConnected,
        SeriesScore seriesScore,
        Role? rematchOffererRole,
        JsonElement? gameOptions)
    {
        Code = code;
        GameId = gameId;
        GameOptions = gameOptions;
        SideSelectionMode = sideSelectionMode;
        CreatedAt = createdAt;
        Host = host;
        Challenger = challenger;
        Status = status;
        CurrentMatch = currentMatch;
        HostConnected = hostConnected;
        ChallengerConnected = challengerConnected;
        SeriesScore = seriesScore;
        RematchOffererRole = rematchOffererRole;
    }

    /// <summary>
    /// Create a brand-new room. Sides under <see cref="SideSelectionMode.HostPicksSpecific"/>
    /// and <see cref="SideSelectionMode.Random"/> must already be resolved
    /// (the application handler does the resolution before construction);
    /// under <see cref="SideSelectionMode.ChallengerPicks"/> the host's side
    /// is null and resolves at join.
    /// <paramref name="gameOptions"/> is the validated per-room options blob
    /// for the game module (Sprint 9 PR1); the handler validates it against
    /// the module before calling this factory. Defaults to null for games
    /// without options.
    /// </summary>
    public static Room Create(
        RoomCode code,
        GameId gameId,
        SideSelectionMode sideSelectionMode,
        Player host,
        DateTimeOffset createdAt,
        JsonElement? gameOptions = null)
    {
        ValidateHostSideForMode(sideSelectionMode, host.Side);

        return new Room(
            code,
            gameId,
            sideSelectionMode,
            createdAt,
            host,
            challenger: null,
            status: RoomStatus.WaitingForOpponent,
            currentMatch: null,
            hostConnected: false,
            challengerConnected: false,
            seriesScore: SeriesScore.Zero,
            rematchOffererRole: null,
            gameOptions: gameOptions);
    }

    /// <summary>
    /// Rehydrate a room snapshot from persistence. Used by the Infrastructure
    /// layer; application code constructs rooms via <see cref="Create"/>.
    /// </summary>
    public static Room Rehydrate(
        RoomCode code,
        GameId gameId,
        SideSelectionMode sideSelectionMode,
        DateTimeOffset createdAt,
        Player host,
        Player? challenger,
        RoomStatus status,
        Match? currentMatch,
        bool hostConnected,
        bool challengerConnected,
        SeriesScore seriesScore,
        Role? rematchOffererRole,
        JsonElement? gameOptions = null) =>
        new(code, gameId, sideSelectionMode, createdAt, host, challenger,
            status, currentMatch, hostConnected, challengerConnected, seriesScore,
            rematchOffererRole, gameOptions);

    /// <summary>
    /// Register the challenger via the join-onboarding endpoint
    /// (CLAUDE.md §2.5 join contract). Resolves both sides if the room is in
    /// <see cref="SideSelectionMode.ChallengerPicks"/> mode; under the other
    /// two modes the challenger's side is derived from the host's.
    /// </summary>
    /// <param name="module">Game module — used to validate sides and compute
    /// the opposite side without coupling the platform to per-game vocab.</param>
    public void RegisterChallenger(
        Player challenger,
        string? challengerPickedSide,
        IGameModule module)
    {
        if (Status is not RoomStatus.WaitingForOpponent)
        {
            throw new DomainException($"Cannot register challenger when room is {Status}.");
        }

        if (Challenger is not null)
        {
            throw new DomainException("Challenger seat is already filled.");
        }

        var resolvedChallengerSide = ResolveChallengerSide(challengerPickedSide, module);
        Challenger = challenger with { Side = resolvedChallengerSide };
    }

    /// <summary>Mark a role's SignalR presence as connected.</summary>
    public void MarkConnected(Role role)
    {
        switch (role)
        {
            case Role.Host: HostConnected = true; break;
            case Role.Challenger: ChallengerConnected = true; break;
        }
    }

    /// <summary>Mark a role's SignalR presence as disconnected.</summary>
    public void MarkDisconnected(Role role)
    {
        switch (role)
        {
            case Role.Host: HostConnected = false; break;
            case Role.Challenger: ChallengerConnected = false; break;
        }
    }

    /// <summary>
    /// Transition WaitingForOpponent onward and create the first match when
    /// both players are registered AND both currently connected via SignalR
    /// (CLAUDE.md §2.9). Setup-less games go straight to
    /// <see cref="RoomStatus.InProgress"/>; games whose module implements
    /// <see cref="ISetupGame"/> enter <see cref="RoomStatus.SettingUp"/>
    /// instead (Sprint 10 seam C) — the match aggregate exists from here
    /// (it holds the accumulating setup state) but the clock only starts
    /// at <see cref="CompleteSetup"/>. Idempotent — calls when the room
    /// isn't ready, or has already left WaitingForOpponent, do nothing.
    /// Returns true if this call performed the transition (the caller
    /// checks <see cref="Status"/> to know which deadline to schedule).
    /// </summary>
    public bool TryStartMatch(IGameModule module, TimeSpan clockBudget, DateTimeOffset now)
    {
        if (Status is not RoomStatus.WaitingForOpponent) return false;
        if (Challenger is null) return false;
        if (!(HostConnected && ChallengerConnected)) return false;

        if (Host.Side is null || Challenger.Side is null)
        {
            throw new DomainException("Both sides must be resolved before a match starts.");
        }

        var firstMover = RoleForSide(module.FirstMoveSide);
        CurrentMatch = Match.Start(
            GameId,
            module.NewMatch(GameOptions),
            module.FirstMoveSide,
            firstMover,
            clockBudget,
            now);
        Status = module is ISetupGame ? RoomStatus.SettingUp : RoomStatus.InProgress;
        return true;
    }

    /// <summary>
    /// Transition <see cref="RoomStatus.SettingUp"/> → <see cref="RoomStatus.InProgress"/>
    /// once the module reports setup complete (Sprint 10 seam C). Re-stamps
    /// the clock so the first mover starts from the full budget — the setup
    /// phase is unclocked.
    /// </summary>
    public void CompleteSetup(DateTimeOffset now)
    {
        if (Status != RoomStatus.SettingUp || CurrentMatch is null)
        {
            throw new DomainException($"Cannot complete setup in status {Status}.");
        }
        CurrentMatch.BeginPlayAfterSetup(now);
        Status = RoomStatus.InProgress;
    }

    /// <summary>
    /// Terminal expiry for a setup phase nobody finished (Sprint 10 seam C:
    /// the setup deadline elapsed with neither side committed). Mirrors the
    /// WaitingForOpponent expiry — no match outcome, no score change; the
    /// room is simply dead.
    /// </summary>
    public void ExpireSetup()
    {
        if (Status != RoomStatus.SettingUp)
        {
            throw new DomainException($"Cannot expire setup in status {Status}.");
        }
        Status = RoomStatus.Expired;
    }

    public Player? PlayerFor(Role role) => role switch
    {
        Role.Host => Host,
        Role.Challenger => Challenger,
        _ => null,
    };

    public string? SideFor(Role role) => PlayerFor(role)?.Side;

    /// <summary>Resolve which role corresponds to a side. Throws if the side isn't assigned.</summary>
    public Role RoleForSide(string side)
    {
        if (Host.Side == side) return Role.Host;
        if (Challenger?.Side == side) return Role.Challenger;
        throw new DomainException($"Side '{side}' is not assigned in this room.");
    }

    /// <summary>
    /// Transition the room to <see cref="RoomStatus.Closed"/> from
    /// <see cref="RoomStatus.Ended"/> or <see cref="RoomStatus.AwaitingRematch"/>
    /// (docs/state.md §2.4). Idempotent on <see cref="RoomStatus.Closed"/> —
    /// double-clicks of "Back to lobby" / racey tab-close-after-exit both land
    /// the room in the same place.
    ///
    /// Returns true on a legal call; <paramref name="transitioned"/> reports
    /// whether the call actually flipped status (so the caller knows whether
    /// to persist + broadcast <c>OpponentExited</c>). Returns false when the
    /// room is in a status that does not permit exit (still waiting / in
    /// progress) — the room is left untouched.
    /// </summary>
    public bool TryExit(out bool transitioned)
    {
        if (Status == RoomStatus.Closed)
        {
            transitioned = false;
            return true;
        }
        if (Status is not (RoomStatus.Ended or RoomStatus.AwaitingRematch))
        {
            transitioned = false;
            return false;
        }
        Status = RoomStatus.Closed;
        transitioned = true;
        return true;
    }

    /// <summary>
    /// First-offer / implicit-accept dispatch for rematch (docs/platform.md
    /// §1 #10). From <see cref="RoomStatus.Ended"/> the caller is recorded
    /// as the offerer and the room transitions to <see cref="RoomStatus.AwaitingRematch"/>
    /// (<paramref name="effect"/> = <see cref="RematchOfferResult.OfferRecorded"/>).
    /// From <see cref="RoomStatus.AwaitingRematch"/> with a different caller,
    /// the call is treated as an implicit accept — near-simultaneous dual offer
    /// resolved under the room lock (<paramref name="effect"/> =
    /// <see cref="RematchOfferResult.ImplicitlyAccepted"/>). Returns false (no
    /// state change) when the room status is wrong or the same caller is
    /// re-offering — cancelling your own offer is not a v1 feature.
    /// </summary>
    public bool TryOfferRematch(
        Role caller, IGameModule module, DateTimeOffset now, out RematchOfferResult effect)
    {
        if (Status == RoomStatus.Ended)
        {
            RematchOffererRole = caller;
            Status = RoomStatus.AwaitingRematch;
            effect = RematchOfferResult.OfferRecorded;
            return true;
        }
        if (Status == RoomStatus.AwaitingRematch && RematchOffererRole != caller)
        {
            AcceptRematchInternal(module, now);
            effect = RematchOfferResult.ImplicitlyAccepted;
            return true;
        }
        effect = default;
        return false;
    }

    /// <summary>
    /// Responder-side accept (docs/platform.md §1 #10 / #15).
    /// Valid only in <see cref="RoomStatus.AwaitingRematch"/> when the
    /// caller is not the original offerer. Swaps host/challenger sides
    /// deterministically and starts a fresh match — the platform skeleton
    /// has no idea about the per-game first-mover; the game module's
    /// <see cref="IGameModule.FirstMoveSide"/> resolves which (now-swapped)
    /// role moves first.
    /// </summary>
    public void AcceptRematch(Role caller, IGameModule module, DateTimeOffset now)
    {
        if (Status != RoomStatus.AwaitingRematch)
        {
            throw new DomainException(
                $"Cannot accept a rematch in status {Status}; expected AwaitingRematch.");
        }
        if (RematchOffererRole is null || RematchOffererRole == caller)
        {
            throw new DomainException("Only the responder may accept a rematch.");
        }
        AcceptRematchInternal(module, now);
    }

    /// <summary>
    /// Responder-side reject. Valid only in <see cref="RoomStatus.AwaitingRematch"/>
    /// when the caller is not the original offerer (docs/platform.md
    /// §1 #10). Transitions directly to <see cref="RoomStatus.Closed"/>;
    /// the rejector's UI auto-routes to the lobby while the offerer stays
    /// with a manual exit and a "declined" notice.
    /// </summary>
    public void RejectRematch(Role caller)
    {
        if (Status != RoomStatus.AwaitingRematch)
        {
            throw new DomainException(
                $"Cannot reject a rematch in status {Status}; expected AwaitingRematch.");
        }
        if (RematchOffererRole is null || RematchOffererRole == caller)
        {
            throw new DomainException("Only the responder may reject a rematch.");
        }
        RematchOffererRole = null;
        Status = RoomStatus.Closed;
    }

    private void AcceptRematchInternal(IGameModule module, DateTimeOffset now)
    {
        if (Challenger is null || Host.Side is null || Challenger.Side is null)
        {
            throw new DomainException(
                "Both players' sides must already be resolved before a rematch can accept.");
        }

        // Deterministic side swap per §1 #15: whoever had X plays O, etc.
        // Works regardless of how sides were originally chosen at room
        // creation — both sides are always resolved by the time we reach
        // AwaitingRematch.
        Host = Host with { Side = module.OtherSide(Host.Side) };
        Challenger = Challenger with { Side = module.OtherSide(Challenger.Side) };

        var firstMover = RoleForSide(module.FirstMoveSide);
        CurrentMatch = Match.Start(
            GameId,
            module.NewMatch(GameOptions),
            module.FirstMoveSide,
            firstMover,
            module.DefaultClockBudget,
            now);

        RematchOffererRole = null;
        // Setup games re-enter the setup phase on every rematch — fresh
        // fleets each match (docs/games/seabattle.md). The side swap above
        // already happened, so the first-shot advantage alternates.
        Status = module is ISetupGame ? RoomStatus.SettingUp : RoomStatus.InProgress;
    }

    /// <summary>End the current match and transition the room to Ended.
    /// Updates the series scoreboard from the just-concluded match's outcome
    /// (docs/platform.md §1 #13).</summary>
    public void EndCurrentMatch()
    {
        if (CurrentMatch is null || !CurrentMatch.IsEnded)
        {
            throw new DomainException("Cannot end a room whose match isn't finished.");
        }
        SeriesScore = ApplyOutcomeToScore(SeriesScore, CurrentMatch.Outcome!);
        Status = RoomStatus.Ended;
    }

    /// <summary>
    /// Translate a terminal outcome into a score update. Wins go to the
    /// winning side's role; resigns and timeouts give the point to the
    /// opposite role of the side that resigned / timed out; draws bump the
    /// shared draw counter. Unknown outcome types throw rather than silently
    /// skipping the update — future outcome subtypes must be wired here
    /// explicitly.
    /// </summary>
    private SeriesScore ApplyOutcomeToScore(SeriesScore score, Outcome outcome) => outcome switch
    {
        Win w => score.WithWin(RoleForSide(w.WinningSide)),
        Resign r => score.WithWin(OtherRole(RoleForSide(r.ResigningSide))),
        Timeout t => score.WithWin(OtherRole(RoleForSide(t.TimedOutSide))),
        Disconnect d => score.WithWin(OtherRole(RoleForSide(d.LosingSide))),
        Draw => score.WithDraw(),
        _ => throw new DomainException(
            $"Unsupported outcome type '{outcome.GetType().Name}' for scoring."),
    };

    private static Role OtherRole(Role role) => role switch
    {
        Role.Host => Role.Challenger,
        Role.Challenger => Role.Host,
        _ => throw new DomainException($"Unknown role '{role}'."),
    };

    private string ResolveChallengerSide(string? challengerPickedSide, IGameModule module)
    {
        switch (SideSelectionMode)
        {
            case SideSelectionMode.HostPicksSpecific:
            case SideSelectionMode.Random:
                if (challengerPickedSide is not null)
                {
                    throw new DomainException(
                        "Challenger may not pick a side under this room's selection mode.");
                }
                if (Host.Side is null)
                {
                    throw new DomainException(
                        "Host side must be resolved at room creation under this mode.");
                }
                return module.OtherSide(Host.Side);

            case SideSelectionMode.ChallengerPicks:
                if (challengerPickedSide is null)
                {
                    throw new DomainException(
                        "Challenger must pick a side under ChallengerPicks mode.");
                }
                if (!module.ValidSides.Contains(challengerPickedSide))
                {
                    throw new DomainException(
                        $"Side '{challengerPickedSide}' is not valid for game '{module.Id}'.");
                }

                // Resolve the host's side now that the challenger has chosen.
                Host = Host with { Side = module.OtherSide(challengerPickedSide) };
                return challengerPickedSide;

            default:
                throw new DomainException($"Unknown side-selection mode {SideSelectionMode}.");
        }
    }

    private static void ValidateHostSideForMode(SideSelectionMode mode, string? hostSide)
    {
        switch (mode)
        {
            case SideSelectionMode.HostPicksSpecific:
            case SideSelectionMode.Random:
                if (hostSide is null)
                {
                    throw new DomainException(
                        "Host side must be provided under this selection mode.");
                }
                break;
            case SideSelectionMode.ChallengerPicks:
                if (hostSide is not null)
                {
                    throw new DomainException(
                        "Host side must be null under ChallengerPicks mode; resolves at join.");
                }
                break;
        }
    }
}
