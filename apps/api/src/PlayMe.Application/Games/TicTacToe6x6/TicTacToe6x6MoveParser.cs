using System.Text.Json;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Dtos;
using PlayMe.Domain.Games.TicTacToe6x6;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Games.TicTacToe6x6;

/// <summary>
/// Wire → domain mapper for Tic-Tac-Toe 6×6 moves. Reads <c>cell</c> off
/// the opaque <see cref="MoveDto.Payload"/>; the platform never inspects
/// the payload shape (CLAUDE.md §7 "Platform thinness"). Both the payload
/// shape and the reject key are agreed between this parser and the TTT-6×6
/// web renderer — see <see cref="TicTacToe6x6Errors"/>. Intentionally
/// independent of the 3×3 and 9×9 parsers per the platform-thinness rule.
/// </summary>
public sealed class TicTacToe6x6MoveParser : IGameMoveParser
{
    public GameId GameId => TicTacToe6x6GameModule.GameId;

    public AppResult<GameMove> Parse(MoveDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.Payload.ValueKind != JsonValueKind.Object ||
            !dto.Payload.TryGetProperty("cell", out var cellEl) ||
            cellEl.ValueKind != JsonValueKind.Number ||
            !cellEl.TryGetInt32(out var cell))
        {
            return AppResult<GameMove>.Fail(
                TicTacToe6x6Errors.ValidationMove,
                "TicTacToe 6×6 move requires a numeric 'cell'.");
        }
        return AppResult<GameMove>.Ok(new TicTacToe6x6Move(cell));
    }
}
