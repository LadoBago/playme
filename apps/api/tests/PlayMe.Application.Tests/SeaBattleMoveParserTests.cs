using System.Text.Json;
using FluentAssertions;
using PlayMe.Application.Dtos;
using PlayMe.Application.Games.SeaBattle;
using PlayMe.Domain.Games.SeaBattle;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Wire-shape parsing for Sea Battle: `{x,y}` shots and `{ships:[…]}` fleet
/// commits. Shape only — rule legality lives in the module and is covered
/// by <see cref="SeaBattleGameModuleTests"/>.
/// </summary>
public sealed class SeaBattleMoveParserTests
{
    private static readonly SeaBattleMoveParser Parser = new();

    private static MoveDto Payload(object payload) =>
        new(JsonSerializer.SerializeToElement(payload));

    [Fact]
    public void Parses_a_shot()
    {
        var result = Parser.Parse(Payload(new { x = 3, y = 7 }));

        result.Succeeded.Should().BeTrue();
        result.Value.Should().Be(new SeaBattleShot(3, 7));
    }

    [Fact]
    public void Parses_a_fleet_commit()
    {
        var result = Parser.Parse(Payload(new
        {
            ships = new[]
            {
                new { x = 0, y = 0, length = 4, horizontal = true },
                new { x = 0, y = 2, length = 1, horizontal = false },
            },
        }));

        result.Succeeded.Should().BeTrue();
        var placement = result.Value.Should().BeOfType<SeaBattleFleetPlacement>().Subject;
        placement.Ships.Should().Equal(
            new SeaBattleShip(0, 0, 4, Horizontal: true),
            new SeaBattleShip(0, 2, 1, Horizontal: false));
    }

    [Theory]
    [InlineData("""{"x": 3}""")]
    [InlineData("""{"y": 3}""")]
    [InlineData("""{"x": "a", "y": 1}""")]
    [InlineData("""{"x": 1.5, "y": 1}""")]
    [InlineData("""{}""")]
    [InlineData("""[]""")]
    [InlineData("\"shot\"")]
    public void Malformed_payloads_are_rejected_with_the_validation_key(string json)
    {
        var result = Parser.Parse(new MoveDto(JsonDocument.Parse(json).RootElement));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(SeaBattleErrors.ValidationMove);
    }

    [Theory]
    [InlineData("""{"ships": "fleet"}""")]
    [InlineData("""{"ships": [{"x": 0, "y": 0, "length": 4}]}""")]
    [InlineData("""{"ships": [{"x": 0, "y": 0, "horizontal": true}]}""")]
    [InlineData("""{"ships": [{"x": 0, "y": 0, "length": "4", "horizontal": true}]}""")]
    [InlineData("""{"ships": [{"x": 0, "y": 0, "length": 4, "horizontal": "yes"}]}""")]
    [InlineData("""{"ships": [42]}""")]
    public void Malformed_fleets_are_rejected_with_the_validation_key(string json)
    {
        var result = Parser.Parse(new MoveDto(JsonDocument.Parse(json).RootElement));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(SeaBattleErrors.ValidationMove);
    }

    [Fact]
    public void Oversized_ships_array_is_rejected_before_per_ship_parsing()
    {
        var ships = Enumerable.Range(0, 11)
            .Select(i => new { x = i, y = 0, length = 1, horizontal = true })
            .ToArray();

        var result = Parser.Parse(Payload(new { ships }));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(SeaBattleErrors.ValidationMove);
    }

    [Fact]
    public void Empty_ships_array_parses_and_fails_module_validation_not_parsing()
    {
        // Composition rules are the module's job — the parser only checks shape.
        var result = Parser.Parse(Payload(new { ships = Array.Empty<object>() }));

        result.Succeeded.Should().BeTrue();
        result.Value.Should().BeOfType<SeaBattleFleetPlacement>()
            .Which.Ships.Should().BeEmpty();
    }
}
