using PlayMe.Domain.Platform;

namespace PlayMe.Domain.Games.Connect4;

/// <summary>
/// A Connect 4 move: a column index 0..6 the player drops their disc into.
/// Gravity lands it on the lowest empty row of that column. The module
/// rejects any other value.
/// </summary>
public sealed record Connect4Move(int Column) : GameMove;
