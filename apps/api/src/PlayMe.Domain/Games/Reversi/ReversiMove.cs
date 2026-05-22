using PlayMe.Domain.Platform;

namespace PlayMe.Domain.Games.Reversi;

/// <summary>
/// Reversi move shapes. The parser interprets the wire payload into one of
/// these; <see cref="ReversiGameModule"/> pattern-matches on the concrete
/// type. The platform sees only the <see cref="GameMove"/> abstract base —
/// pass vocabulary stays inside this module (CLAUDE.md §7 "Platform
/// thinness").
/// </summary>
public abstract record ReversiMove : GameMove;

/// <summary>A normal placement at (<paramref name="Row"/>, <paramref name="Col"/>).</summary>
public sealed record ReversiPlacement(int Row, int Col) : ReversiMove;

/// <summary>
/// A pass. Emitted by the Reversi renderer when the server-published
/// <c>mustPassSide</c> flag indicates the player has no legal placements;
/// re-validated server-side by <see cref="ReversiGameModule"/> against the
/// current board.
/// </summary>
public sealed record ReversiPass : ReversiMove;
