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
    public SideSelectionMode SideSelectionMode { get; }
    public DateTimeOffset CreatedAt { get; }

    public Player Host { get; private set; }
    public Player? Challenger { get; private set; }

    public RoomStatus Status { get; private set; }
    public Match? CurrentMatch { get; private set; }

    public bool HostConnected { get; private set; }
    public bool ChallengerConnected { get; private set; }

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
        bool challengerConnected)
    {
        Code = code;
        GameId = gameId;
        SideSelectionMode = sideSelectionMode;
        CreatedAt = createdAt;
        Host = host;
        Challenger = challenger;
        Status = status;
        CurrentMatch = currentMatch;
        HostConnected = hostConnected;
        ChallengerConnected = challengerConnected;
    }

    /// <summary>
    /// Create a brand-new room. Sides under <see cref="SideSelectionMode.HostPicksSpecific"/>
    /// and <see cref="SideSelectionMode.Random"/> must already be resolved
    /// (the application handler does the resolution before construction);
    /// under <see cref="SideSelectionMode.ChallengerPicks"/> the host's side
    /// is null and resolves at join.
    /// </summary>
    public static Room Create(
        RoomCode code,
        GameId gameId,
        SideSelectionMode sideSelectionMode,
        Player host,
        DateTimeOffset createdAt)
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
            challengerConnected: false);
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
        bool challengerConnected) =>
        new(code, gameId, sideSelectionMode, createdAt, host, challenger,
            status, currentMatch, hostConnected, challengerConnected);

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
    /// Transition WaitingForOpponent → InProgress and create the first match
    /// when both players are registered AND both currently connected via
    /// SignalR (CLAUDE.md §2.9). Idempotent — calls when the room isn't
    /// ready, or is already in progress, do nothing.
    /// </summary>
    public void TryStartMatch(IGameModule module)
    {
        if (Status is not RoomStatus.WaitingForOpponent) return;
        if (Challenger is null) return;
        if (!(HostConnected && ChallengerConnected)) return;

        if (Host.Side is null || Challenger.Side is null)
        {
            throw new DomainException("Both sides must be resolved before a match starts.");
        }

        CurrentMatch = Match.Start(GameId, module.NewMatch(), module.FirstMoveSide);
        Status = RoomStatus.InProgress;
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

    /// <summary>End the current match and transition the room to Ended.</summary>
    public void EndCurrentMatch()
    {
        if (CurrentMatch is null || !CurrentMatch.IsEnded)
        {
            throw new DomainException("Cannot end a room whose match isn't finished.");
        }
        Status = RoomStatus.Ended;
    }

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
