using System.Text.Json;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Dtos;
using PlayMe.Domain.Games.Reversi;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Games.Reversi;

/// <summary>
/// Wire → domain mapper for Reversi moves. One payload shape is accepted:
/// a placement (<c>{ "row": int, "col": int }</c>) → <see cref="ReversiPlacement"/>.
/// Forced skips are resolved server-side via <c>MoveResult.KeepTurn</c> and
/// never appear on the wire. The platform never inspects the payload shape
/// (CLAUDE.md §7 "Platform thinness"); both the payload shape and the
/// reject keys are agreed between this parser and the Reversi web
/// renderer — see <see cref="ReversiErrors"/>.
/// </summary>
public sealed class ReversiMoveParser : IGameMoveParser
{
    public GameId GameId => ReversiGameModule.GameId;

    public AppResult<GameMove> Parse(MoveDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.Payload.ValueKind != JsonValueKind.Object)
        {
            return AppResult<GameMove>.Fail(
                ReversiErrors.ValidationMove, "Reversi move payload must be a JSON object.");
        }

        if (!dto.Payload.TryGetProperty("row", out var rowEl) ||
            rowEl.ValueKind != JsonValueKind.Number ||
            !rowEl.TryGetInt32(out var row) ||
            !dto.Payload.TryGetProperty("col", out var colEl) ||
            colEl.ValueKind != JsonValueKind.Number ||
            !colEl.TryGetInt32(out var col))
        {
            return AppResult<GameMove>.Fail(
                ReversiErrors.ValidationMove,
                "Reversi move requires numeric 'row' and 'col'.");
        }

        return AppResult<GameMove>.Ok(new ReversiPlacement(row, col));
    }
}
