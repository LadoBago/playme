using PlayMe.Application.Abstractions;
using PlayMe.Application.Dtos;
using PlayMe.Application.Errors;
using PlayMe.Domain.Games.TicTacToe3x3;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Games.TicTacToe3x3;

/// <summary>
/// Wire → domain mapper for Tic-Tac-Toe moves. Rejects payloads without
/// <c>Cell</c> set (the game's only meaningful field) and lets the rules
/// module judge range/legality.
/// </summary>
public sealed class TicTacToeMoveParser : IGameMoveParser
{
    public GameId GameId => TicTacToe3x3GameModule.GameId;

    public AppResult<GameMove> Parse(MoveDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.Cell is null)
        {
            return AppResult<GameMove>.Fail(
                ErrorCode.ValidationMove, "TicTacToe move requires 'cell'.");
        }
        return AppResult<GameMove>.Ok(new TicTacToeMove(dto.Cell.Value));
    }
}
