using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using PlayMe.Application.Commands.PlayBotMove;
using PlayMe.Application.Commands.SubmitMove;
using PlayMe.Application.Dtos;
using PlayMe.Application.Errors;
using PlayMe.Application.Telemetry;
using PlayMe.Application.Tests.Fakes;
using PlayMe.Application.Time;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Seam D (Sprint 12): the bot's move pipeline against the compatibility
/// module. Contract under test — per docs/bots.md and
/// docs/roadmap/sprint-12-bots.md: the bot moves only on its turn, its
/// move goes through the same commit tail as a human move (clock,
/// timeout, scoring, analytics), a retained turn re-enrolls the bot, a
/// hidden-state bot sees only its own projection, a decision made against
/// a match that moved on is discarded, and a module fault forfeits.
/// </summary>
public sealed class PlayBotMoveHandlerTests
{
    private sealed class Fixture
    {
        public FakeBotGameModule Module { get; } = new();
        public FakeRoomRepository Rooms { get; } = new();
        public FakeClock Clock { get; } = new();
        public RecordingTimeoutScheduler Timeouts { get; } = new();
        public RecordingGraceScheduler Graces { get; } = new();
        public RecordingAnalyticsClient Analytics { get; } = new();
        public RecordingBotTurnScheduler BotTurns { get; } = new();

        public Room Seed(string hostSide = FakeBotGameModule.First)
        {
            var room = BotRoomFactory.Started(Module, Clock.UtcNow, hostSide);
            Rooms.Seed(room);
            return room;
        }

        public PlayBotMoveHandler BotMove() => new(
            Rooms,
            BotRoomFactory.Registry(Module),
            Clock,
            new ClockService(),
            new SeededRandom(),
            TestHandlers.MoveCommitter(Rooms, Timeouts, Graces, Analytics, BotTurns),
            NullLogger<PlayBotMoveHandler>.Instance);

        public Task<AppResult<SubmitMoveResult>> HostSteps(int n) =>
            TestHandlers.SubmitMove(
                    Rooms, BotRoomFactory.Registry(Module), Clock, Timeouts, Graces,
                    analytics: Analytics, botTurns: BotTurns)
                .HandleAsync(
                    new SubmitMoveCommand(
                        RoomFactory.RoomCodeValue,
                        RoomFactory.HostPlayerId,
                        Role.Host,
                        new MoveDto(JsonSerializer.SerializeToElement(new { n }))),
                    CancellationToken.None);

        public Task<AppResult<PlayBotMoveResult>> Fire() =>
            BotMove().HandleAsync(new PlayBotMoveCommand(RoomFactory.RoomCodeValue), CancellationToken.None);

        public async Task<Room> Load() =>
            (await Rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), CancellationToken.None))!;
    }

    [Fact]
    public async Task Human_move_enrolls_the_bot_after_the_think_delay()
    {
        var f = new Fixture();
        f.Seed();

        (await f.HostSteps(1)).Succeeded.Should().BeTrue();

        f.BotTurns.Scheduled.Should().ContainSingle()
            .Which.DueAt.Should().Be(f.Clock.UtcNow + PlatformConstants.BotThinkDelay);
    }

    [Fact]
    public async Task Bot_moves_on_its_turn_and_hands_the_turn_back()
    {
        var f = new Fixture();
        f.Seed();
        await f.HostSteps(1);
        f.Timeouts.Scheduled.Clear();

        var result = await f.Fire();

        result.Succeeded.Should().BeTrue();
        result.Value!.Applied.Should().BeTrue();
        result.Value.MatchEnded.Should().BeFalse();
        var room = await f.Load();
        room.CurrentMatch!.SideToMove.Should().Be(FakeBotGameModule.First);
        room.CurrentMatch.MoveCount.Should().Be(2);
        FakeBotGameModule.CounterOf(room.CurrentMatch.State, FakeBotGameModule.Second).Should().Be(1);
        f.Timeouts.Scheduled.Should().ContainSingle("the human's clock is now running");
        f.Module.SeenLevels.Should().Equal(BotLevel.Medium);
    }

    [Fact]
    public async Task Bot_does_nothing_while_the_human_is_on_the_move()
    {
        var f = new Fixture();
        f.Seed();

        var result = await f.Fire();

        result.Value!.Applied.Should().BeFalse();
        (await f.Load()).CurrentMatch!.MoveCount.Should().Be(0);
        f.Module.SeenStates.Should().BeEmpty("the bot is not even asked");
    }

    [Fact]
    public async Task Bot_sees_only_its_own_projection_of_a_hidden_state()
    {
        var f = new Fixture();
        f.Seed();
        await f.HostSteps(1);

        await f.Fire();

        var seen = f.Module.SeenStates.Should().ContainSingle().Subject;
        FakeBotGameModule.CounterOf(seen, FakeBotGameModule.First).Should().Be(FakeBotGameModule.Masked);
        FakeBotGameModule.CounterOf(seen, FakeBotGameModule.Second).Should().Be(0);
    }

    [Fact]
    public async Task Retained_turn_enrolls_the_bot_again()
    {
        var f = new Fixture();
        f.Seed();
        f.Module.ChooseMoveOverride = (_, _, _, _) => new FakeBotGameModule.Step(2);
        await f.HostSteps(1);
        f.BotTurns.Scheduled.Clear();

        var result = await f.Fire();

        result.Value!.Applied.Should().BeTrue();
        (await f.Load()).CurrentMatch!.SideToMove.Should().Be(FakeBotGameModule.Second);
        f.BotTurns.Scheduled.Should().ContainSingle();
    }

    [Fact]
    public async Task Winning_move_ends_the_match_through_the_shared_tail()
    {
        var f = new Fixture();
        f.Seed();
        f.Module.ChooseMoveOverride = (_, _, _, _) => new FakeBotGameModule.Step(2);
        await f.HostSteps(1);

        await f.Fire(); // 2
        await f.Fire(); // 4
        var result = await f.Fire(); // 6 ≥ Target

        result.Value!.MatchEnded.Should().BeTrue();
        var room = await f.Load();
        room.Status.Should().Be(RoomStatus.Ended);
        room.CurrentMatch!.Outcome.Should().BeOfType<Win>()
            .Which.WinningSide.Should().Be(FakeBotGameModule.Second);
        room.SeriesScore.Challenger.Should().Be(1);
        f.Timeouts.Cancelled.Should().Contain(RoomFactory.RoomCodeValue);
        f.Analytics.Events.Should().Contain(e => e.Event == AnalyticsEvents.MatchEnded);
        f.BotTurns.Scheduled.Should().HaveCount(3,
            "the human's move and the two retained turns each enrolled the bot; the winning move enrols nothing");
    }

    [Fact]
    public async Task Illegal_move_from_the_module_forfeits_the_bot()
    {
        var f = new Fixture();
        f.Seed();
        f.Module.ChooseMoveOverride = (_, _, _, _) => new FakeBotGameModule.Step(9);
        await f.HostSteps(1);

        var result = await f.Fire();

        result.Succeeded.Should().BeTrue();
        result.Value!.Forfeited.Should().BeTrue();
        result.Value.MatchEnded.Should().BeTrue();
        var room = await f.Load();
        room.CurrentMatch!.Outcome.Should().BeOfType<Resign>()
            .Which.ResigningSide.Should().Be(FakeBotGameModule.Second);
        room.SeriesScore.Host.Should().Be(1);
    }

    [Fact]
    public async Task Throwing_module_forfeits_the_bot()
    {
        var f = new Fixture();
        f.Seed();
        f.Module.ChooseMoveOverride = (_, _, _, _) => throw new InvalidOperationException("bot bug");
        await f.HostSteps(1);

        var result = await f.Fire();

        result.Value!.Forfeited.Should().BeTrue();
        (await f.Load()).CurrentMatch!.Outcome.Should().BeOfType<Resign>();
    }

    [Fact]
    public async Task Decision_made_while_the_match_moved_on_is_discarded()
    {
        var f = new Fixture();
        f.Seed();
        await f.HostSteps(1);
        f.Module.OnThinking = async () =>
        {
            // The human resigns while the bot is thinking (outside the lock).
            var room = await f.Load();
            room.CurrentMatch!.Resign(FakeBotGameModule.First);
            room.EndCurrentMatch();
            await f.Rooms.SaveAsync(room, CancellationToken.None);
        };

        var result = await f.Fire();

        result.Value!.Applied.Should().BeFalse();
        var after = await f.Load();
        after.CurrentMatch!.Outcome.Should().BeOfType<Resign>();
        after.CurrentMatch.MoveCount.Should().Be(1, "the stale bot move never landed");
    }

    [Fact]
    public async Task Bot_whose_clock_ran_out_loses_on_time_instead_of_moving()
    {
        var f = new Fixture();
        f.Seed();
        await f.HostSteps(1);
        f.Clock.Advance(TimeSpan.FromMinutes(2));

        var result = await f.Fire();

        result.Value!.TimedOut.Should().BeTrue();
        result.Value.MatchEnded.Should().BeTrue();
        (await f.Load()).CurrentMatch!.Outcome.Should().BeOfType<Domain.Platform.Timeout>()
            .Which.TimedOutSide.Should().Be(FakeBotGameModule.Second);
    }

    [Fact]
    public async Task Bot_seat_cannot_submit_through_the_human_pipeline()
    {
        var f = new Fixture();
        f.Seed();
        await f.HostSteps(1);

        var result = await TestHandlers.SubmitMove(
                f.Rooms, BotRoomFactory.Registry(f.Module), f.Clock, f.Timeouts)
            .HandleAsync(
                new SubmitMoveCommand(
                    RoomFactory.RoomCodeValue,
                    BotRoomFactory.BotPlayerId,
                    Role.Challenger,
                    new MoveDto(JsonSerializer.SerializeToElement(new { n = 1 }))),
                CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(PlatformErrors.SessionUnauthorized);
    }
}
