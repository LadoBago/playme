using FluentAssertions;
using PlayMe.Infrastructure.Security;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// <see cref="RoomCodeRedactor"/> is the boundary between raw 128-bit
/// invite tokens and the logging pipeline (docs/security.md §8). The
/// contract: deterministic (so log lines on the same room correlate),
/// collision-resistant enough to be useful, and prefixed so the field is
/// obvious in log queries.
/// </summary>
public sealed class RoomCodeRedactorTests
{
    private readonly RoomCodeRedactor _redactor = new();

    [Fact]
    public void Same_input_yields_same_output()
    {
        const string code = "abc123XYZ-_45";

        var first = _redactor.Redact(code);
        var second = _redactor.Redact(code);

        first.Should().Be(second);
    }

    [Fact]
    public void Different_inputs_yield_different_outputs()
    {
        var a = _redactor.Redact("room-alpha");
        var b = _redactor.Redact("room-bravo");

        a.Should().NotBe(b);
    }

    [Fact]
    public void Output_is_prefixed_with_rc()
    {
        var redacted = _redactor.Redact("any-room-code");

        redacted.Should().StartWith("rc:");
        redacted.Should().HaveLength("rc:".Length + 8); // 32 bits = 8 hex chars
    }
}
