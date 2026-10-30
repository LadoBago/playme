using System.Text.Json;
using FluentAssertions;
using PlayMe.Application.Dtos;
using PlayMe.Application.Games.Checkers;
using PlayMe.Domain.Games.Checkers;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Unit tests for <see cref="CheckersMoveParser"/>. The platform calls this
/// with an opaque <see cref="MoveDto.Payload"/>; the parser is the only
/// place the <c>{"from":{row,col},"to":{row,col}}</c> shape lives on the
/// API side (CLAUDE.md §7 "Platform thinness"). It checks shape only —
/// bounds and legality belong to the module.
/// </summary>
public sealed class CheckersMoveParserTests
{
    private readonly CheckersMoveParser _parser = new();

    private static MoveDto DtoFrom(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return new MoveDto(doc.RootElement.Clone());
    }

    [Fact]
    public void GameId_is_checkers()
    {
        _parser.GameId.Value.Should().Be("checkers");
    }

    [Fact]
    public void Parse_accepts_valid_move()
    {
        var result = _parser.Parse(DtoFrom("""{"from":{"row":5,"col":2},"to":{"row":4,"col":3}}"""));

        result.Succeeded.Should().BeTrue();
        var move = result.Value.Should().BeOfType<CheckersMove>().Which;
        move.From.Should().Be(new CheckersCoordinate(5, 2));
        move.To.Should().Be(new CheckersCoordinate(4, 3));
    }

    [Fact]
    public void Parse_passes_out_of_range_coordinates_through_for_the_module_to_reject()
    {
        var result = _parser.Parse(DtoFrom("""{"from":{"row":5,"col":2},"to":{"row":4,"col":-1}}"""));

        result.Succeeded.Should().BeTrue();
        ((CheckersMove)result.Value!).To.Col.Should().Be(-1);
    }

    [Theory]
    [InlineData("""{"from":{"row":5,"col":2}}""", "missing 'to'")]
    [InlineData("""{"to":{"row":4,"col":3}}""", "missing 'from'")]
    [InlineData("""{"from":[5,2],"to":{"row":4,"col":3}}""", "'from' not an object")]
    [InlineData("""{"from":{"row":5},"to":{"row":4,"col":3}}""", "missing 'col'")]
    [InlineData("""{"from":{"row":"5","col":2},"to":{"row":4,"col":3}}""", "non-numeric coordinate")]
    [InlineData("""{"from":{"row":5.5,"col":2},"to":{"row":4,"col":3}}""", "non-integer coordinate")]
    [InlineData("""{"path":[{"row":5,"col":2},{"row":3,"col":4}]}""", "whole-chain payload is not a move")]
    [InlineData("[5,2,4,3]", "non-object payload")]
    public void Parse_rejects_malformed_payloads(string json, string because)
    {
        var result = _parser.Parse(DtoFrom(json));

        result.Succeeded.Should().BeFalse(because);
        result.Error.Should().Be(CheckersErrors.ValidationMove);
    }
}
