namespace PlayMe.Domain.Games.TicTacToe;

/// <summary>
/// Per-room options accepted by the unified <c>tictactoe</c> module
/// (Sprint 9 PR1b). <see cref="BoardSize"/> must be 3, 6, or 9 — any other
/// value is rejected by <c>TicTacToeGameModule.ValidateOptions</c>. The
/// derived <c>winLength</c> (3→3, 6→4, 9→5) is not part of the wire shape;
/// the module computes it from <see cref="BoardSize"/>.
/// </summary>
public sealed record TicTacToeOptions(int BoardSize);
