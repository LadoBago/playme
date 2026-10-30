using PlayMe.Domain.Platform;

namespace PlayMe.Domain.Games.Checkers;

/// <summary>
/// A single Checkers action: one step or one jump by the piece at
/// <paramref name="From"/> to <paramref name="To"/>. A multi-capture chain
/// is never a single move — each jump is submitted separately and the
/// module retains the turn via <see cref="Platform.MoveResult.KeepTurn"/>
/// while the chain is open (see <see cref="CheckersState.Chain"/>). The
/// platform sees only the <see cref="GameMove"/> abstract base.
/// </summary>
public sealed record CheckersMove(CheckersCoordinate From, CheckersCoordinate To) : GameMove;
