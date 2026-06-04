using PlayMe.Domain.Platform;

namespace PlayMe.Domain.Games.SeaBattle;

/// <summary>
/// Sea Battle action shapes. The parser interprets the wire payload into
/// one of these; <see cref="SeaBattleGameModule"/> pattern-matches on the
/// concrete type. The platform sees only the <see cref="GameMove"/>
/// abstract base — shot and fleet vocabulary stays inside this module
/// (CLAUDE.md §7 "Platform thinness").
/// </summary>
public abstract record SeaBattleMove : GameMove;

/// <summary>A shot at cell (<paramref name="X"/>, <paramref name="Y"/>) of the opponent's grid.</summary>
public sealed record SeaBattleShot(int X, int Y) : SeaBattleMove;

/// <summary>
/// The one-and-final fleet commit for the setup phase (platform seam C).
/// Routed through <c>SubmitSetup</c>, never <c>SubmitMove</c>; the module
/// rejects it as a move payload during battle.
/// </summary>
public sealed record SeaBattleFleetPlacement(IReadOnlyList<SeaBattleShip> Ships) : SeaBattleMove;
