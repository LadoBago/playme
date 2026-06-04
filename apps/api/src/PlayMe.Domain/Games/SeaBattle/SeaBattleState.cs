using PlayMe.Domain.Platform;

namespace PlayMe.Domain.Games.SeaBattle;

/// <summary>
/// Full (unprojected) Sea Battle state: both fleets and both shot lists.
/// A null fleet means that side hasn't committed setup yet — the match is
/// in the setup phase until both are present. Shots are kept in firing
/// order; results (miss / hit / sunk) are derived against the opposing
/// fleet, never stored. This full shape exists only server-side and on the
/// post-match reveal — live wire payloads go through
/// <see cref="SeaBattleGameModule.SerializeFor"/> (platform seam A).
/// </summary>
public sealed record SeaBattleState(
    IReadOnlyList<SeaBattleShip>? FirstFleet,
    IReadOnlyList<SeaBattleShip>? SecondFleet,
    IReadOnlyList<SeaBattleCoordinate> ShotsByFirst,
    IReadOnlyList<SeaBattleCoordinate> ShotsBySecond) : IGameState
{
    public const int GridSize = 10;

    /// <summary>Total fleet cells per side: 1×4 + 2×3 + 3×2 + 4×1.</summary>
    public const int FleetCellCount = 20;

    public static SeaBattleState Empty { get; } = new(
        FirstFleet: null,
        SecondFleet: null,
        ShotsByFirst: Array.Empty<SeaBattleCoordinate>(),
        ShotsBySecond: Array.Empty<SeaBattleCoordinate>());

    public bool SetupComplete => FirstFleet is not null && SecondFleet is not null;

    public IReadOnlyList<SeaBattleShip>? FleetOf(string side) =>
        side == SeaBattleSides.First ? FirstFleet : SecondFleet;

    public IReadOnlyList<SeaBattleCoordinate> ShotsBy(string side) =>
        side == SeaBattleSides.First ? ShotsByFirst : ShotsBySecond;
}
