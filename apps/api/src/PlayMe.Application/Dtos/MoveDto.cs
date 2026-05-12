namespace PlayMe.Application.Dtos;

/// <summary>
/// Wire-level move payload. Sprint 1 has only Tic-Tac-Toe, which uses
/// <see cref="Cell"/> (0..8, row-major). Connect 4 (Sprint 3) will add a
/// <c>Column</c> field; per-game parsers (<c>IGameMoveParser</c>) interpret
/// the relevant fields and reject moves with fields that don't match the
/// game's shape.
/// </summary>
public sealed record MoveDto(int? Cell);
