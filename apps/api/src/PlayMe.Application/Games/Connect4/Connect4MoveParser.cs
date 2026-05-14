using System.Text.Json;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Dtos;
using PlayMe.Domain.Games.Connect4;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Games.Connect4;

/// <summary>
/// Wire → domain mapper for Connect 4 moves. Reads <c>column</c> off the
/// opaque <see cref="MoveDto.Payload"/>; the platform never inspects the
/// payload shape (CLAUDE.md §7 "Platform thinness"). Both the payload shape
/// and the reject keys are agreed between this parser and the Connect 4 web
/// renderer — see <see cref="Connect4Errors"/>.
/// </summary>
public sealed class Connect4MoveParser : IGameMoveParser
{
    public GameId GameId => Connect4GameModule.GameId;

    public AppResult<GameMove> Parse(MoveDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.Payload.ValueKind != JsonValueKind.Object ||
            !dto.Payload.TryGetProperty("column", out var colEl) ||
            colEl.ValueKind != JsonValueKind.Number ||
            !colEl.TryGetInt32(out var column))
        {
            return AppResult<GameMove>.Fail(
                Connect4Errors.ValidationMove, "Connect 4 move requires a numeric 'column'.");
        }
        return AppResult<GameMove>.Ok(new Connect4Move(column));
    }
}
