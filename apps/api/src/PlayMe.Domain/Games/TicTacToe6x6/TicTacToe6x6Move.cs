using PlayMe.Domain.Platform;

namespace PlayMe.Domain.Games.TicTacToe6x6;

/// <summary>
/// A Tic-Tac-Toe 6×6 move: cell index 0..35 in row-major order
/// (row = Cell / 6, col = Cell % 6). The module rejects any other value.
/// </summary>
public sealed record TicTacToe6x6Move(int Cell) : GameMove;
