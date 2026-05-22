using PlayMe.Domain.Platform;

namespace PlayMe.Domain.Games.TicTacToe;

/// <summary>
/// A unified-Tic-Tac-Toe move: cell index in row-major order on the
/// host-chosen N×N board (Sprint 9 PR1b). <c>Cell</c> is 0..(N²-1); the
/// module rejects any other value (see <see cref="TicTacToeErrors.IllegalCell"/>).
/// </summary>
public sealed record TicTacToeMove(int Cell) : GameMove;
