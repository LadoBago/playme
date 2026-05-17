using System.Text.Json;
using FluentAssertions;
using PlayMe.Application.Dtos;
using PlayMe.Application.Games.TicTacToe9x9;
using PlayMe.Domain.Games.TicTacToe9x9;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Wire-shape contract tests for <see cref="TicTacToe9x9MoveParser"/>.
/// Covers the agreed payload shape (<c>{ "cell": int }</c>) between the
/// parser and the per-game web renderer (CLAUDE.md §7 "Platform thinness").
/// Per-module duplication of test shapes is acceptable — these are
/// intentionally independent of the 3×3 parser's tests.
/// </summary>
public sealed class TicTacToe9x9MoveParserTests
{
    private readonly TicTacToe9x9MoveParser _parser = new();

    [Fact]
    public void Parse_accepts_numeric_cell()
    {
        var dto = MakeDto("""{"cell":42}""");

        var result = _parser.Parse(dto);

        result.Succeeded.Should().BeTrue();
        result.Value.Should().BeOfType<TicTacToe9x9Move>()
            .Which.Cell.Should().Be(42);
    }

    [Fact]
    public void Parse_rejects_missing_cell()
    {
        var dto = MakeDto("""{}""");

        var result = _parser.Parse(dto);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(TicTacToe9x9Errors.ValidationMove);
    }

    [Fact]
    public void Parse_rejects_non_numeric_cell()
    {
        var dto = MakeDto("""{"cell":"oops"}""");

        var result = _parser.Parse(dto);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(TicTacToe9x9Errors.ValidationMove);
    }

    [Fact]
    public void Parse_rejects_non_object_payload()
    {
        var dto = MakeDto("""[1,2,3]""");

        var result = _parser.Parse(dto);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(TicTacToe9x9Errors.ValidationMove);
    }

    [Fact]
    public void Parse_rejects_fractional_cell()
    {
        // The parser uses TryGetInt32, so a fractional number must fail
        // rather than silently truncate.
        var dto = MakeDto("""{"cell":1.5}""");

        var result = _parser.Parse(dto);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(TicTacToe9x9Errors.ValidationMove);
    }

    private static MoveDto MakeDto(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return new MoveDto(doc.RootElement.Clone());
    }
}
