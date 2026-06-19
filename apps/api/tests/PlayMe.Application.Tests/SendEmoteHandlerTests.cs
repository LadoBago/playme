using FluentAssertions;
using PlayMe.Application.Commands.SendEmote;
using PlayMe.Application.Errors;
using PlayMe.Application.RateLimiting;
using PlayMe.Application.Tests.Fakes;
using PlayMe.Domain.Games.TicTacToe;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Application-layer behaviour of the in-match emote relay: allowlist
/// validation, membership authorization, the status gate (active play +
/// post-game only), and the silent-suppression paths (rate limit, missing
/// room, out-of-phase). The handler mutates nothing, so there is no saved
/// state to assert — only the returned <see cref="SendEmoteEffect"/>.
/// </summary>
public sealed class SendEmoteHandlerTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromMinutes(3);
    private const string ValidEmote = "smile";

    private static SendEmoteHandler Build(
        FakeRoomRepository rooms, RecordingRateLimiter? limiter = null) =>
        new(rooms, limiter ?? new RecordingRateLimiter());

    private static SendEmoteCommand Command(string emoteId = ValidEmote) =>
        new(RoomFactory.RoomCodeValue, RoomFactory.HostPlayerId, Role.Host, emoteId);

    [Fact]
    public async Task Delivers_when_match_in_progress()
    {
        var rooms = new FakeRoomRepository();
        rooms.Seed(RoomFactory.InProgress(new FakeClock().UtcNow, Budget));

        var result = await Build(rooms).HandleAsync(Command(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.Effect.Should().Be(SendEmoteEffect.Delivered);
    }

    [Fact]
    public async Task Delivers_after_match_ended_post_game_screen()
    {
        var clock = new FakeClock();
        var rooms = new FakeRoomRepository();
        rooms.Seed(EndedRoom(clock));

        var result = await Build(rooms).HandleAsync(Command(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.Effect.Should().Be(SendEmoteEffect.Delivered);
    }

    [Fact]
    public async Task Unknown_emote_id_fails_without_spending_rate_budget()
    {
        var rooms = new FakeRoomRepository();
        rooms.Seed(RoomFactory.InProgress(new FakeClock().UtcNow, Budget));
        var limiter = new RecordingRateLimiter();

        var result = await Build(rooms, limiter).HandleAsync(
            Command("definitely-not-an-emote"), CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(PlatformErrors.EmoteUnknown);
        // Rejected before the rate-limit check or any Redis round-trip.
        limiter.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task Suppressed_during_setup_phase_or_before_opponent_joins()
    {
        var rooms = new FakeRoomRepository();
        rooms.Seed(RoomFactory.WaitingForOpponent(new FakeClock().UtcNow));

        var result = await Build(rooms).HandleAsync(Command(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.Effect.Should().Be(SendEmoteEffect.Suppressed);
    }

    [Fact]
    public async Task Suppressed_when_rate_limited()
    {
        var rooms = new FakeRoomRepository();
        rooms.Seed(RoomFactory.InProgress(new FakeClock().UtcNow, Budget));
        var limiter = new RecordingRateLimiter { AllowNext = false };

        var result = await Build(rooms, limiter).HandleAsync(Command(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.Effect.Should().Be(SendEmoteEffect.Suppressed);
        limiter.Calls.Should().ContainSingle()
            .Which.Policy.Should().Be(SessionRateLimitPolicies.Emote);
    }

    [Fact]
    public async Task Suppressed_when_room_not_found()
    {
        var result = await Build(new FakeRoomRepository())
            .HandleAsync(Command(), CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Value!.Effect.Should().Be(SendEmoteEffect.Suppressed);
    }

    [Fact]
    public async Task Fails_unauthorized_when_player_id_does_not_match_role()
    {
        var rooms = new FakeRoomRepository();
        rooms.Seed(RoomFactory.InProgress(new FakeClock().UtcNow, Budget));

        var result = await Build(rooms).HandleAsync(
            new SendEmoteCommand(
                RoomFactory.RoomCodeValue, "someone-else", Role.Host, ValidEmote),
            CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(PlatformErrors.SessionUnauthorized);
    }

    private static Room EndedRoom(FakeClock clock)
    {
        var room = RoomFactory.InProgress(clock.UtcNow, Budget);
        room.CurrentMatch!.Resign(TicTacToeSides.X);
        room.EndCurrentMatch();
        return room;
    }
}
