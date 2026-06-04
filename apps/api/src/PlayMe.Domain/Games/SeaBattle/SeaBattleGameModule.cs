using System.Text.Json;
using System.Text.Json.Serialization;
using PlayMe.Domain.Platform;

namespace PlayMe.Domain.Games.SeaBattle;

/// <summary>
/// Sea Battle (ka „ჩაძირობანა") — post-Soviet ruleset on a 10×10 grid per
/// player (<see href="../../../../../docs/games/seabattle.md">games/seabattle.md</see>):
/// a hidden fleet of 10 ships (1×4 + 2×3 + 3×2 + 4×1, straight lines, no
/// two ships touching — not even diagonally), shots answered miss / hit /
/// sunk, **a hit or sunk earns another shot** (seam B
/// <see cref="MoveResult.KeepTurn"/>), win when all 20 of the opponent's
/// ship cells are hit. A draw is impossible.
///
/// <para>
/// First hidden-information game: the module implements
/// <see cref="IHiddenStateGame"/> (seam A) so live wire payloads carry a
/// per-viewer projection — own fleet plus both sides' public knowledge
/// (shots with results, sunk ships) — and <see cref="ISetupGame"/>
/// (seam C) for the secret simultaneous fleet placement, bounded by a
/// 2-minute setup budget. <see cref="Serialize"/>'s full shape appears on
/// the wire only once the match is terminal.
/// </para>
/// </summary>
public sealed class SeaBattleGameModule : IGameModule, IHiddenStateGame, ISetupGame
{
    public static readonly GameId GameId = new("seabattle");

    private static readonly string[] ValidSidesArray = { SeaBattleSides.First, SeaBattleSides.Second };

    /// <summary>Required ship count per length: 1×4-decker, 2×3, 3×2, 4×1.</summary>
    private static readonly IReadOnlyDictionary<int, int> RequiredShipCounts =
        new Dictionary<int, int> { [4] = 1, [3] = 2, [2] = 3, [1] = 4 };

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public GameId Id => GameId;

    public IReadOnlyList<string> ValidSides => ValidSidesArray;

    public string FirstMoveSide => SeaBattleSides.First;

    public TimeSpan DefaultClockBudget { get; } = TimeSpan.FromMinutes(10);

    public TimeSpan SetupBudget { get; } = TimeSpan.FromMinutes(2);

    public string OtherSide(string side) => side switch
    {
        SeaBattleSides.First => SeaBattleSides.Second,
        SeaBattleSides.Second => SeaBattleSides.First,
        _ => throw new ArgumentException($"Unknown side '{side}'.", nameof(side)),
    };

    public string? ValidateOptions(JsonElement? options) =>
        options is null ? null : "errors.config.invalidGameOptions";

    public IGameState NewMatch(JsonElement? options) => SeaBattleState.Empty;

    // --- Setup phase (seam C) -------------------------------------------

    public string? ValidateSetup(IGameState state, string side, GameMove setup)
    {
        var board = Cast(state);
        if (setup is not SeaBattleFleetPlacement placement)
        {
            return SeaBattleErrors.ValidationMove;
        }
        if (board.FleetOf(side) is not null)
        {
            // One commit per side is platform-enforced before validation;
            // reaching here with a fleet already stored is a contract bug.
            throw new InvalidOperationException(
                $"Side '{side}' already has a fleet — the platform must reject double commits.");
        }
        return IsLegalFleet(placement.Ships) ? null : SeaBattleErrors.InvalidFleet;
    }

    public IGameState ApplySetup(IGameState state, string side, GameMove setup)
    {
        var board = Cast(state);
        var placement = (SeaBattleFleetPlacement)setup;
        return side == SeaBattleSides.First
            ? board with { FirstFleet = placement.Ships }
            : board with { SecondFleet = placement.Ships };
    }

    public bool IsSetupComplete(IGameState state) => Cast(state).SetupComplete;

    // --- Battle phase ------------------------------------------------------

    public MoveResult ApplyMove(IGameState state, string side, GameMove move)
    {
        var board = Cast(state);
        if (side != SeaBattleSides.First && side != SeaBattleSides.Second)
        {
            throw new ArgumentException($"Unknown side '{side}'.", nameof(side));
        }
        if (move is not SeaBattleShot shot)
        {
            // A fleet placement routed through SubmitMove — wrong phase.
            return MoveResult.Reject(SeaBattleErrors.ValidationMove);
        }
        if (!board.SetupComplete)
        {
            // SubmitMove gates on InProgress, which a setup game only
            // reaches once both fleets are committed.
            throw new InvalidOperationException(
                "Shot applied before setup completed — the platform must gate moves on InProgress.");
        }

        if (!InBounds(shot.X, shot.Y))
        {
            return MoveResult.Reject(SeaBattleErrors.OutOfBounds);
        }

        var cell = new SeaBattleCoordinate(shot.X, shot.Y);
        var priorShots = board.ShotsBy(side);
        if (priorShots.Contains(cell))
        {
            return MoveResult.Reject(SeaBattleErrors.AlreadyShot);
        }

        var newShots = priorShots.Append(cell).ToArray();
        var newBoard = side == SeaBattleSides.First
            ? board with { ShotsByFirst = newShots }
            : board with { ShotsBySecond = newShots };

        var opponentFleet = board.FleetOf(OtherSide(side))!;
        var hit = opponentFleet.Any(ship => ship.Cells().Contains(cell));
        var won = hit && CountHits(opponentFleet, newShots) == SeaBattleState.FleetCellCount;

        return MoveResult.Accept(
            newBoard,
            ending: won ? new Win(side) : null,
            keepTurn: hit);
    }

    // --- Serialization (persistence + terminal reveal) ----------------------

    public string Serialize(IGameState state)
    {
        var board = Cast(state);
        var payload = new StatePayload(
            board.FirstFleet,
            board.SecondFleet,
            board.ShotsByFirst,
            board.ShotsBySecond);
        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    public IGameState Deserialize(string serialized)
    {
        ArgumentNullException.ThrowIfNull(serialized);
        StatePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<StatePayload>(serialized, SerializerOptions);
        }
        catch (JsonException e)
        {
            throw new ArgumentException("Failed to parse Sea Battle state.", nameof(serialized), e);
        }
        if (payload is null)
        {
            throw new ArgumentException("Sea Battle state blob was null.", nameof(serialized));
        }
        return new SeaBattleState(
            payload.FirstFleet,
            payload.SecondFleet,
            payload.ShotsByFirst ?? Array.Empty<SeaBattleCoordinate>(),
            payload.ShotsBySecond ?? Array.Empty<SeaBattleCoordinate>());
    }

    // --- Per-viewer projection (seam A) ---------------------------------------

    /// <summary>
    /// Live wire view: everything that is public knowledge between the two
    /// players (both sides' shots with derived miss/hit/sunk results, and
    /// the opponent ships each side has sunk — a sunk announcement reveals
    /// the whole ship) plus, when <paramref name="viewerSide"/> is a player,
    /// that player's own fleet. A null viewer (the anonymous room snapshot)
    /// gets the public knowledge only. The opponent's surviving fleet never
    /// appears in any projection — the platform ships the full
    /// <see cref="Serialize"/> shape once the match is terminal.
    /// </summary>
    public string SerializeFor(IGameState state, string? viewerSide)
    {
        var board = Cast(state);
        var payload = new ProjectedPayload(
            Phase: board.SetupComplete ? "battle" : "setup",
            ViewerSide: viewerSide,
            YourFleet: viewerSide is null ? null : board.FleetOf(viewerSide),
            Shots: new SideSplit<IReadOnlyList<ShotView>>(
                First: ShotViews(board.ShotsByFirst, board.SecondFleet),
                Second: ShotViews(board.ShotsBySecond, board.FirstFleet)),
            Sunk: new SideSplit<IReadOnlyList<SeaBattleShip>>(
                First: SunkShips(board.SecondFleet, board.ShotsByFirst),
                Second: SunkShips(board.FirstFleet, board.ShotsBySecond)));
        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    // --- Fleet validation -------------------------------------------------------

    /// <summary>
    /// Composition check for one fleet: exactly the required multiset of
    /// ship lengths, every cell in bounds, and no two ships overlapping or
    /// touching (Chebyshev distance ≥ 2 between cells of distinct ships).
    /// </summary>
    private static bool IsLegalFleet(IReadOnlyList<SeaBattleShip> ships)
    {
        if (ships.Count != RequiredShipCounts.Values.Sum())
        {
            return false;
        }

        foreach (var (length, required) in RequiredShipCounts)
        {
            if (ships.Count(s => s.Length == length) != required)
            {
                return false;
            }
        }

        var cellsPerShip = ships.Select(s => s.Cells().ToArray()).ToArray();
        if (cellsPerShip.Any(cells => cells.Any(c => !InBounds(c.X, c.Y))))
        {
            return false;
        }

        for (var i = 0; i < cellsPerShip.Length; i++)
        {
            for (var j = i + 1; j < cellsPerShip.Length; j++)
            {
                foreach (var a in cellsPerShip[i])
                {
                    foreach (var b in cellsPerShip[j])
                    {
                        if (Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)) <= 1)
                        {
                            return false;
                        }
                    }
                }
            }
        }

        return true;
    }

    private static bool InBounds(int x, int y) =>
        x >= 0 && x < SeaBattleState.GridSize && y >= 0 && y < SeaBattleState.GridSize;

    private static int CountHits(
        IReadOnlyList<SeaBattleShip> fleet, IReadOnlyList<SeaBattleCoordinate> shots)
    {
        var shotSet = new HashSet<SeaBattleCoordinate>(shots);
        return fleet.Sum(ship => ship.Cells().Count(shotSet.Contains));
    }

    /// <summary>
    /// Derive one shooter's shot list with results against the defending
    /// fleet. A shot is <c>sunk</c> when the ship it hit has every cell
    /// shot — earlier hits on that ship retroactively read as sunk too,
    /// matching how the paper game marks a finished ship.
    /// </summary>
    private static ShotView[] ShotViews(
        IReadOnlyList<SeaBattleCoordinate> shots, IReadOnlyList<SeaBattleShip>? defendingFleet)
    {
        if (shots.Count == 0)
        {
            return Array.Empty<ShotView>();
        }

        var shotSet = new HashSet<SeaBattleCoordinate>(shots);
        var views = new ShotView[shots.Count];
        for (var i = 0; i < shots.Count; i++)
        {
            var shot = shots[i];
            var result = "miss";
            if (defendingFleet is not null)
            {
                foreach (var ship in defendingFleet)
                {
                    if (!ship.Cells().Contains(shot)) continue;
                    result = ship.Cells().All(shotSet.Contains) ? "sunk" : "hit";
                    break;
                }
            }
            views[i] = new ShotView(shot.X, shot.Y, result);
        }
        return views;
    }

    /// <summary>Defending-fleet ships that the given shot list has fully sunk.</summary>
    private static SeaBattleShip[] SunkShips(
        IReadOnlyList<SeaBattleShip>? defendingFleet, IReadOnlyList<SeaBattleCoordinate> shots)
    {
        if (defendingFleet is null || shots.Count == 0)
        {
            return Array.Empty<SeaBattleShip>();
        }
        var shotSet = new HashSet<SeaBattleCoordinate>(shots);
        return defendingFleet.Where(ship => ship.Cells().All(shotSet.Contains)).ToArray();
    }

    private static SeaBattleState Cast(IGameState state) =>
        state as SeaBattleState
        ?? throw new ArgumentException(
            $"Expected {nameof(SeaBattleState)}, got {state.GetType().Name}.", nameof(state));

    /// <summary>Persisted / terminal-reveal shape. Per-game and opaque to the platform.</summary>
    private sealed record StatePayload(
        IReadOnlyList<SeaBattleShip>? FirstFleet,
        IReadOnlyList<SeaBattleShip>? SecondFleet,
        IReadOnlyList<SeaBattleCoordinate>? ShotsByFirst,
        IReadOnlyList<SeaBattleCoordinate>? ShotsBySecond);

    /// <summary>One shot with its derived result: <c>miss</c> / <c>hit</c> / <c>sunk</c>.</summary>
    private sealed record ShotView(int X, int Y, string Result);

    /// <summary>Per-side pair used by the projected view.</summary>
    private sealed record SideSplit<T>(T First, T Second);

    /// <summary>
    /// Live wire shape for one viewer (seam A projection). Renderer
    /// contract — see docs/games/seabattle.md.
    /// </summary>
    private sealed record ProjectedPayload(
        string Phase,
        string? ViewerSide,
        IReadOnlyList<SeaBattleShip>? YourFleet,
        SideSplit<IReadOnlyList<ShotView>> Shots,
        SideSplit<IReadOnlyList<SeaBattleShip>> Sunk);
}
