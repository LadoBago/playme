using PlayMe.Domain.Platform;

namespace PlayMe.Domain.Games.Reversi;

/// <summary>
/// Reversi move shapes. The parser interprets the wire payload into one of
/// these; <see cref="ReversiGameModule"/> pattern-matches on the concrete
/// type. The platform sees only the <see cref="GameMove"/> abstract base.
/// A forced skip (the opponent has no legal move) is not a move — the
/// module resolves it synchronously via
/// <see cref="Platform.MoveResult.KeepTurn"/> (seam B).
/// </summary>
public abstract record ReversiMove : GameMove;

/// <summary>A normal placement at (<paramref name="Row"/>, <paramref name="Col"/>).</summary>
public sealed record ReversiPlacement(int Row, int Col) : ReversiMove;
