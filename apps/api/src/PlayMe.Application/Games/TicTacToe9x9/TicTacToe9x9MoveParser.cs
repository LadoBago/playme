using System.Text.Json;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Dtos;
using PlayMe.Domain.Games.TicTacToe9x9;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Games.TicTacToe9x9;

/// <summary>
/// Wire → domain mapper for Tic-Tac-Toe 9×9 moves. Reads <c>cell</c> off the
/// opaque <see cref="MoveDto.Payload"/>; the platform never inspects the
/// payload shape (CLAUDE.md §7 "Platform thinness"). Both the payload shape
/// and the reject key are agreed between this parser and the TTT-9×9 web
/// renderer — see <see cref="TicTacToe9x9Errors"/>. Independent of (and
/// intentionally not shared with) the analogous parser in the Tic-Tac-Toe
/// 3×3 module — per-module duplication is acceptable.
/// </summary>
public sealed class TicTacToe9x9MoveParser : IGameMoveParser
{
    public GameId GameId => TicTacToe9x9GameModule.GameId;

    public AppResult<GameMove> Parse(MoveDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.Payload.ValueKind != JsonValueKind.Object ||
            !dto.Payload.TryGetProperty("cell", out var cellEl) ||
            cellEl.ValueKind != JsonValueKind.Number ||
            !cellEl.TryGetInt32(out var cell))
        {
            return AppResult<GameMove>.Fail(
                TicTacToe9x9Errors.ValidationMove, "TicTacToe 9×9 move requires a numeric 'cell'.");
        }
        return AppResult<GameMove>.Ok(new TicTacToe9x9Move(cell));
    }
}
