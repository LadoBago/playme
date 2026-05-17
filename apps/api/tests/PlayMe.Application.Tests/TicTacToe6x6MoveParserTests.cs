using System.Text.Json;
using FluentAssertions;
using PlayMe.Application.Dtos;
using PlayMe.Application.Games.TicTacToe6x6;
using PlayMe.Domain.Games.TicTacToe6x6;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Unit tests for <see cref="TicTacToe6x6MoveParser"/>. The platform calls
/// this with an opaque <see cref="MoveDto.Payload"/> and the parser is the
/// only place the <c>{"cell": int}</c> shape lives on the API side
/// (CLAUDE.md §7 "Platform thinness").
/// </summary>
public sealed class TicTacToe6x6MoveParserTests
{
    private readonly TicTacToe6x6MoveParser _parser = new();

    private static MoveDto DtoFrom(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return new MoveDto(doc.RootElement.Clone());
    }

    [Fact]
    public void GameId_is_tictactoe_6x6()
    {
        _parser.GameId.Value.Should().Be("tictactoe-6x6");
    }

    [Fact]
    public void Parse_accepts_valid_cell()
    {
        var result = _parser.Parse(DtoFrom("""{"cell":17}"""));

        result.Succeeded.Should().BeTrue();
        result.Value.Should().BeOfType<TicTacToe6x6Move>()
            .Which.Cell.Should().Be(17);
    }

    [Fact]
    public void Parse_rejects_missing_cell()
    {
        var result = _parser.Parse(DtoFrom("""{"foo":3}"""));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(TicTacToe6x6Errors.ValidationMove);
    }

    [Fact]
    public void Parse_rejects_non_numeric_cell()
    {
        var result = _parser.Parse(DtoFrom("""{"cell":"oops"}"""));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(TicTacToe6x6Errors.ValidationMove);
    }

    [Fact]
    public void Parse_rejects_non_object_payload()
    {
        var result = _parser.Parse(DtoFrom("17"));

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(TicTacToe6x6Errors.ValidationMove);
    }
}
