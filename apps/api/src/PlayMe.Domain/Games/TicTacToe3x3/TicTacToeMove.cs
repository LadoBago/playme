using PlayMe.Domain.Platform;

namespace PlayMe.Domain.Games.TicTacToe3x3;

/// <summary>
/// A Tic-Tac-Toe move: cell index 0..8 in row-major order
/// (row = Cell / 3, col = Cell % 3). The module rejects any other value.
/// </summary>
public sealed record TicTacToeMove(int Cell) : GameMove;
