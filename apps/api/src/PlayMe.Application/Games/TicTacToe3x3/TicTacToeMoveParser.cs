using System.Text.Json;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Dtos;
using PlayMe.Domain.Games.TicTacToe3x3;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Games.TicTacToe3x3;

/// <summary>
/// Wire → domain mapper for Tic-Tac-Toe moves. Reads <c>cell</c> off the
/// opaque <see cref="MoveDto.Payload"/>; the platform never inspects the
/// payload shape (CLAUDE.md §7 "Platform thinness"). Both the payload shape
/// and the reject key are agreed between this parser and the TTT web
/// renderer — see <see cref="TicTacToeErrors"/>.
/// </summary>
public sealed class TicTacToeMoveParser : IGameMoveParser
{
    public GameId GameId => TicTacToe3x3GameModule.GameId;

    public AppResult<GameMove> Parse(MoveDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.Payload.ValueKind != JsonValueKind.Object ||
            !dto.Payload.TryGetProperty("cell", out var cellEl) ||
            cellEl.ValueKind != JsonValueKind.Number ||
            !cellEl.TryGetInt32(out var cell))
        {
            return AppResult<GameMove>.Fail(
                TicTacToeErrors.ValidationMove, "TicTacToe move requires a numeric 'cell'.");
        }
        return AppResult<GameMove>.Ok(new TicTacToeMove(cell));
    }
}
