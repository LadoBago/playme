using PlayMe.Domain.Platform;

namespace PlayMe.Domain.Games.TicTacToe9x9;

/// <summary>
/// A Tic-Tac-Toe 9×9 move: cell index 0..80 in row-major order
/// (row = Cell / 9, col = Cell % 9). The module rejects any other value.
/// Independent of (and intentionally not shared with) the analogous type in
/// the Tic-Tac-Toe 3×3 module — per-module duplication is acceptable
/// (CLAUDE.md §7 "Platform thinness").
/// </summary>
public sealed record TicTacToe9x9Move(int Cell) : GameMove;
