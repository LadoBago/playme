using System.Text.Json;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Dtos;
using PlayMe.Domain.Games.Checkers;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Games.Checkers;

/// <summary>
/// Wire → domain mapper for Checkers moves. One payload shape is accepted:
/// <c>{ "from": { "row": int, "col": int }, "to": { "row": int, "col": int } }</c>
/// → <see cref="CheckersMove"/>. A multi-capture chain is submitted one
/// jump at a time; there is no whole-chain payload. This parser checks
/// shape only — bounds, ownership and rule legality are the module's job.
/// The platform never inspects the payload (CLAUDE.md §7 "Platform
/// thinness"); the shape and the reject keys are agreed between this
/// parser and the Checkers web renderer — see <see cref="CheckersErrors"/>.
/// </summary>
public sealed class CheckersMoveParser : IGameMoveParser
{
    public GameId GameId => CheckersGameModule.GameId;

    public AppResult<GameMove> Parse(MoveDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.Payload.ValueKind != JsonValueKind.Object)
        {
            return AppResult<GameMove>.Fail(
                CheckersErrors.ValidationMove, "Checkers move payload must be a JSON object.");
        }

        if (!TryGetCoordinate(dto.Payload, "from", out var from) ||
            !TryGetCoordinate(dto.Payload, "to", out var to))
        {
            return AppResult<GameMove>.Fail(
                CheckersErrors.ValidationMove,
                "Checkers move requires 'from' and 'to' objects with numeric 'row' and 'col'.");
        }

        return AppResult<GameMove>.Ok(new CheckersMove(from, to));
    }

    private static bool TryGetCoordinate(JsonElement obj, string property, out CheckersCoordinate value)
    {
        value = default;
        if (!obj.TryGetProperty(property, out var el) || el.ValueKind != JsonValueKind.Object)
        {
            return false;
        }
        if (!TryGetInt(el, "row", out var row) || !TryGetInt(el, "col", out var col))
        {
            return false;
        }
        value = new CheckersCoordinate(row, col);
        return true;
    }

    private static bool TryGetInt(JsonElement obj, string property, out int value)
    {
        value = 0;
        return obj.TryGetProperty(property, out var el) &&
            el.ValueKind == JsonValueKind.Number &&
            el.TryGetInt32(out value);
    }
}
