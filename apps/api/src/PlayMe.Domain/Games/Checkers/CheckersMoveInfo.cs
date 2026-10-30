namespace PlayMe.Domain.Games.Checkers;

/// <summary>
/// A resolved step or jump: the piece at <paramref name="From"/> moves to
/// <paramref name="To"/>, capturing the piece at <paramref name="Captured"/>
/// (null for a plain step). Used both for the published
/// <see cref="CheckersState.LegalMoves"/> list the renderer highlights from
/// and for <see cref="CheckersState.LastMove"/>.
/// </summary>
public sealed record CheckersMoveInfo(
    CheckersCoordinate From,
    CheckersCoordinate To,
    CheckersCoordinate? Captured = null);
