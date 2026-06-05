using System.Text.Json;
using FluentAssertions;
using PlayMe.Application.Dtos;
using PlayMe.Application.Games.Reversi;
using PlayMe.Domain.Games.Reversi;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Unit tests for <see cref="ReversiMoveParser"/>. The platform calls this
/// with an opaque <see cref="MoveDto.Payload"/> and the parser is the only
/// place the <c>{"row": int, "col": int}</c> shape lives on the API side
/// (CLAUDE.md §7 "Platform thinness"). The retired <c>{"pass": true}</c>
/// payload from the Sprint 8 synthetic-pass pattern must be rejected like
/// any other malformed move — forced skips are server-resolved via
/// <c>MoveResult.KeepTurn</c> and never appear on the wire.
/// </summary>
public sealed class ReversiMoveParserTests
{
    private readonly ReversiMoveParser _parser = new();

    private static MoveDto DtoFrom(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return new MoveDto(doc.RootElement.Clone());
    }

    [Fact]
    public void GameId_is_reversi()
    {
        _parser.GameId.Value.Should().Be("reversi");
    }

    [Fact]
    public void Parse_accepts_valid_placement()
    {
        var result = _parser.Parse(DtoFrom("""{"row":3,"col":4}"""));

        result.Succeeded.Should().BeTrue();
        var placement = result.Value.Should().BeOfType<ReversiPlacement>().Which;
        placement.Row.Should().Be(3);
        placement.Col.Should().Be(4);
    }

    [Fact]
    public void Parse_rejects_retired_pass_payload()
    {
        var result = _parser.Parse(DtoFrom("""{"pass":true}"""));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(ReversiErrors.ValidationMove);
    }

    [Fact]
    public void Parse_rejects_missing_coordinates()
    {
        var result = _parser.Parse(DtoFrom("""{"row":3}"""));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(ReversiErrors.ValidationMove);
    }

    [Fact]
    public void Parse_rejects_non_numeric_coordinates()
    {
        var result = _parser.Parse(DtoFrom("""{"row":"a","col":4}"""));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(ReversiErrors.ValidationMove);
    }

    [Fact]
    public void Parse_rejects_non_object_payload()
    {
        var result = _parser.Parse(DtoFrom("[3,4]"));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(ReversiErrors.ValidationMove);
    }
}
