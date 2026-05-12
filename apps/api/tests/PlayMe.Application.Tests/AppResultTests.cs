using FluentAssertions;
using PlayMe.Application;
using PlayMe.Application.Errors;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Smoke tests for <see cref="AppResult{T}"/>. Their job is mainly to
/// prove the test toolchain is wired (xUnit + FluentAssertions + CI
/// discovery); they double as a tiny regression net for the result type
/// every handler returns.
/// </summary>
public sealed class AppResultTests
{
    [Fact]
    public void Ok_carries_value_and_no_error()
    {
        var result = AppResult<int>.Ok(42);

        result.Succeeded.Should().BeTrue();
        result.Value.Should().Be(42);
        result.Error.Should().BeNull();
        result.Detail.Should().BeNull();
    }

    [Fact]
    public void Fail_carries_error_and_no_value()
    {
        var result = AppResult<int>.Fail(ErrorCode.RoomNotFound, detail: "for diagnostics");

        result.Succeeded.Should().BeFalse();
        result.Value.Should().Be(default);
        result.Error.Should().Be(ErrorCode.RoomNotFound);
        result.Detail.Should().Be("for diagnostics");
    }

    [Fact]
    public void ToFailure_propagates_error_to_a_different_value_type()
    {
        var failure = AppResult<int>.Fail(ErrorCode.RoomBusy, detail: "lock timeout");

        var propagated = failure.ToFailure<string>();

        propagated.Succeeded.Should().BeFalse();
        propagated.Error.Should().Be(ErrorCode.RoomBusy);
        propagated.Detail.Should().Be("lock timeout");
    }

    [Fact]
    public void ToFailure_on_a_successful_result_throws()
    {
        var ok = AppResult<int>.Ok(1);

        var act = () => ok.ToFailure<string>();

        act.Should().Throw<InvalidOperationException>();
    }
}
