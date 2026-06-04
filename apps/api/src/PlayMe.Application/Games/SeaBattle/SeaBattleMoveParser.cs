using System.Text.Json;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Dtos;
using PlayMe.Domain.Games.SeaBattle;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Games.SeaBattle;

/// <summary>
/// Wire → domain mapper for Sea Battle actions. Two payload shapes are
/// accepted: a shot (<c>{ "x": int, "y": int }</c>) → <see cref="SeaBattleShot"/>,
/// and a fleet commit
/// (<c>{ "ships": [{ "x", "y", "length", "horizontal" }, …] }</c>) →
/// <see cref="SeaBattleFleetPlacement"/>. The platform never inspects the
/// payload (CLAUDE.md §7 "Platform thinness"); shapes and reject keys are
/// agreed between this parser and the Sea Battle web renderer — see
/// <see cref="SeaBattleErrors"/>. This parser checks shape only; rule
/// legality (bounds, duplicates, fleet composition) is the module's job.
/// </summary>
public sealed class SeaBattleMoveParser : IGameMoveParser
{
    /// <summary>Hard cap on the ships array length, parsed before any per-ship
    /// work — a flood-sized array must not cost allocation proportional to
    /// its claim. The legal fleet is exactly 10.</summary>
    private const int MaxShips = 10;

    public GameId GameId => SeaBattleGameModule.GameId;

    public AppResult<GameMove> Parse(MoveDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.Payload.ValueKind != JsonValueKind.Object)
        {
            return AppResult<GameMove>.Fail(
                SeaBattleErrors.ValidationMove, "Sea Battle payload must be a JSON object.");
        }

        if (dto.Payload.TryGetProperty("ships", out var shipsEl))
        {
            return ParseFleet(shipsEl);
        }

        if (TryGetInt(dto.Payload, "x", out var x) && TryGetInt(dto.Payload, "y", out var y))
        {
            return AppResult<GameMove>.Ok(new SeaBattleShot(x, y));
        }

        return AppResult<GameMove>.Fail(
            SeaBattleErrors.ValidationMove,
            "Sea Battle payload requires numeric 'x' and 'y' (shot) or a 'ships' array (setup).");
    }

    private static AppResult<GameMove> ParseFleet(JsonElement shipsEl)
    {
        if (shipsEl.ValueKind != JsonValueKind.Array || shipsEl.GetArrayLength() > MaxShips)
        {
            return AppResult<GameMove>.Fail(
                SeaBattleErrors.ValidationMove,
                $"'ships' must be an array of at most {MaxShips} ships.");
        }

        var ships = new List<SeaBattleShip>(shipsEl.GetArrayLength());
        foreach (var shipEl in shipsEl.EnumerateArray())
        {
            if (shipEl.ValueKind != JsonValueKind.Object ||
                !TryGetInt(shipEl, "x", out var x) ||
                !TryGetInt(shipEl, "y", out var y) ||
                !TryGetInt(shipEl, "length", out var length) ||
                !shipEl.TryGetProperty("horizontal", out var horizontalEl) ||
                horizontalEl.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                return AppResult<GameMove>.Fail(
                    SeaBattleErrors.ValidationMove,
                    "Each ship requires numeric 'x', 'y', 'length' and boolean 'horizontal'.");
            }
            ships.Add(new SeaBattleShip(x, y, length, horizontalEl.GetBoolean()));
        }

        return AppResult<GameMove>.Ok(new SeaBattleFleetPlacement(ships));
    }

    private static bool TryGetInt(JsonElement obj, string property, out int value)
    {
        value = 0;
        return obj.TryGetProperty(property, out var el) &&
            el.ValueKind == JsonValueKind.Number &&
            el.TryGetInt32(out value);
    }
}
