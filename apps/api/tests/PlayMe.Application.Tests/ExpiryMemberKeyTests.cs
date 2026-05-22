using FluentAssertions;
using PlayMe.Domain.Platform;
using PlayMe.Infrastructure.Scheduling;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// <see cref="ExpiryMemberKey"/> is the contract between the room-expiry
/// scheduler (which writes <c>{roomCode}|{gameId}</c> sorted-set members)
/// and the sweeper (which reads them back). The two must agree exactly —
/// if they diverge, scheduled expiries silently never fire room_expired.
/// </summary>
public sealed class ExpiryMemberKeyTests
{
    [Theory]
    [InlineData("ABCDEF", "tictactoe")]
    [InlineData("xyz123", "connect4")]
    [InlineData("Z9Y8X7", "reversi")]
    public void Encode_decode_round_trip(string codeValue, string gameIdValue)
    {
        var encoded = ExpiryMemberKey.Encode(
            new RoomCode(codeValue),
            new GameId(gameIdValue));

        var ok = ExpiryMemberKey.TryDecode(encoded, out var decodedCode, out var decodedGameId);

        ok.Should().BeTrue();
        decodedCode.Should().Be(codeValue);
        decodedGameId.Should().Be(gameIdValue);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-separator")]
    [InlineData("|bare-leading-pipe")]
    [InlineData("trailing-pipe|")]
    public void TryDecode_rejects_malformed_members(string member)
    {
        var ok = ExpiryMemberKey.TryDecode(member, out _, out _);
        ok.Should().BeFalse();
    }
}
