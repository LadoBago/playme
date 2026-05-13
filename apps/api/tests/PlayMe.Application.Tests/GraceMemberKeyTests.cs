using FluentAssertions;
using PlayMe.Domain.Platform;
using PlayMe.Infrastructure.Scheduling;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// <see cref="GraceMemberKey"/> is the contract between the disconnect-grace
/// scheduler (which writes <c>{roomCode}:{role}</c> sorted-set members) and
/// the sweeper (which reads them back). The schedulers and sweepers must
/// agree exactly — if they diverge, scheduled grace entries silently never
/// fire. These tests pin the round-trip.
/// </summary>
public sealed class GraceMemberKeyTests
{
    [Theory]
    [InlineData("ABCDEF", Role.Host)]
    [InlineData("xyz123", Role.Challenger)]
    public void Encode_decode_round_trip(string codeValue, Role role)
    {
        var encoded = GraceMemberKey.Encode(new RoomCode(codeValue), role);

        var ok = GraceMemberKey.TryDecode(encoded, out var decodedCode, out var decodedRole);

        ok.Should().BeTrue();
        decodedCode.Should().Be(codeValue);
        decodedRole.Should().Be(role);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-colon")]
    [InlineData(":bare-leading-colon")]
    [InlineData("trailing-colon:")]
    [InlineData("ABC:Spectator")] // role enum has only Host / Challenger
    public void TryDecode_rejects_malformed_members(string member)
    {
        var ok = GraceMemberKey.TryDecode(member, out _, out _);
        ok.Should().BeFalse();
    }
}
