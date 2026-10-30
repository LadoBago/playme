namespace PlayMe.Domain.Games.Checkers;

/// <summary>
/// An open multi-capture chain: the side to move must continue capturing
/// with the piece now at <paramref name="At"/>. <paramref name="Captured"/>
/// lists the pieces jumped so far this turn — they stay on the board until
/// the chain ends (Turkish strike), still block movement, and cannot be
/// jumped a second time. Persisted with the state so a reconnect mid-chain
/// restores the obligation.
/// </summary>
public sealed record CheckersChain(
    CheckersCoordinate At,
    IReadOnlyList<CheckersCoordinate> Captured);
