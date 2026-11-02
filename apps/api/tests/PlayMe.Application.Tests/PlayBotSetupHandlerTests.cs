using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using PlayMe.Application.Bots;
using PlayMe.Application.Commands.PlayBotSetup;
using PlayMe.Application.Commands.SubmitSetup;
using PlayMe.Application.Dtos;
using PlayMe.Application.Errors;
using PlayMe.Application.Tests.Fakes;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Seam D (Sprint 12) for setup games: the bot commits its setup through
/// the same <c>SetupCommitter</c> tail as a human, the completing commit
/// starts play and enrolls the bot when it moves first, and a module
/// whose setup fails its own validation is logged and left uncommitted
/// (no forfeit exists during setup — docs/state.md §2.1).
/// </summary>
public sealed class PlayBotSetupHandlerTests
{
    private sealed class Fixture
    {
        public FakeBotSetupGameModule Module { get; } = new();
        public FakeRoomRepository Rooms { get; } = new();
        public FakeClock Clock { get; } = new();
        public RecordingTimeoutScheduler Timeouts { get; } = new();
        public RecordingSetupDeadlineScheduler SetupDeadlines { get; } = new();
        public RecordingBotTurnScheduler BotTurns { get; } = new();

        public Room Seed(string hostSide = FakeBotGameModule.First)
        {
            var room = BotRoomFactory.Started(Module, Clock.UtcNow, hostSide);
            Rooms.Seed(room);
            return room;
        }

        public PlayBotSetupHandler BotSetup() => new(
            Rooms,
            BotRoomFactory.Registry(Module),
            Clock,
            new SeededRandom(),
            TestHandlers.SetupCommitter(Rooms, Timeouts, SetupDeadlines, BotTurns),
            NullLogger<PlayBotSetupHandler>.Instance);

        public Task<AppResult<PlayBotSetupResult>> Fire() =>
            BotSetup().HandleAsync(new PlayBotSetupCommand(RoomFactory.RoomCodeValue), CancellationToken.None);

        public Task<AppResult<SubmitSetupResult>> HostCommits(int at) =>
            TestHandlers.SubmitSetup(
                    Rooms, BotRoomFactory.Registry(Module), Clock, Timeouts, SetupDeadlines,
                    botTurns: BotTurns)
                .HandleAsync(
                    new SubmitSetupCommand(
                        RoomFactory.RoomCodeValue,
                        RoomFactory.HostPlayerId,
                        Role.Host,
                        new MoveDto(JsonSerializer.SerializeToElement(new { at }))),
                    CancellationToken.None);

        public async Task<Room> Load() =>
            (await Rooms.LoadAsync(new RoomCode(RoomFactory.RoomCodeValue), CancellationToken.None))!;
    }

    [Fact]
    public void A_fresh_setup_room_owes_the_bot_a_setup()
    {
        var f = new Fixture();
        var room = f.Seed();

        room.Status.Should().Be(RoomStatus.SettingUp);
        BotTurnPolicy.PendingAction(room).Should().Be(BotAction.Setup);
    }

    [Fact]
    public async Task Bot_commits_its_setup_and_waits_for_the_human()
    {
        var f = new Fixture();
        f.Seed();

        var result = await f.Fire();

        result.Succeeded.Should().BeTrue();
        result.Value!.Applied.Should().BeTrue();
        result.Value.MatchStarted.Should().BeFalse();
        var room = await f.Load();
        room.Status.Should().Be(RoomStatus.SettingUp);
        room.CurrentMatch!.ChallengerSetupCommitted.Should().BeTrue();
        room.CurrentMatch.HostSetupCommitted.Should().BeFalse();
        f.BotTurns.Scheduled.Should().BeEmpty("nothing is owed until the human commits");
    }

    [Fact]
    public async Task Second_fire_is_a_no_op_once_committed()
    {
        var f = new Fixture();
        f.Seed();
        await f.Fire();

        var again = await f.Fire();

        again.Value!.Applied.Should().BeFalse();
    }

    [Fact]
    public async Task Human_commit_after_the_bot_starts_play_with_the_human_first()
    {
        var f = new Fixture();
        f.Seed(hostSide: FakeBotGameModule.First);
        await f.Fire();

        var result = await f.HostCommits(1);

        result.Value!.MatchStarted.Should().BeTrue();
        var room = await f.Load();
        room.Status.Should().Be(RoomStatus.InProgress);
        room.CurrentMatch!.SideToMove.Should().Be(FakeBotGameModule.First);
        f.SetupDeadlines.Cancelled.Should().Contain(RoomFactory.RoomCodeValue);
        f.Timeouts.Scheduled.Should().ContainSingle();
        f.BotTurns.Scheduled.Should().BeEmpty("the human moves first");
    }

    [Fact]
    public async Task Bot_commit_that_completes_setup_enrolls_the_bot_when_it_moves_first()
    {
        var f = new Fixture();
        f.Seed(hostSide: FakeBotGameModule.Second); // bot plays "first", the first mover
        await f.HostCommits(2);
        f.BotTurns.Scheduled.Should().ContainSingle("the human's commit re-enrolls the bot's pending setup");
        f.BotTurns.Scheduled.Clear();

        var result = await f.Fire();

        result.Value!.MatchStarted.Should().BeTrue();
        (await f.Load()).Status.Should().Be(RoomStatus.InProgress);
        f.BotTurns.Scheduled.Should().ContainSingle()
            .Which.DueAt.Should().Be(f.Clock.UtcNow + PlatformConstants.BotThinkDelay);
    }

    [Fact]
    public async Task Human_commit_while_the_bot_still_owes_setup_re_enrolls_the_bot()
    {
        var f = new Fixture();
        f.Seed();

        await f.HostCommits(0);

        f.BotTurns.Scheduled.Should().ContainSingle("the planner re-asks after every state change");
    }

    [Fact]
    public async Task Setup_the_module_itself_rejects_is_a_fault_and_stays_uncommitted()
    {
        var f = new Fixture();
        f.Seed();
        f.Module.ChooseSetupOverride = (_, _, _, _) => new FakeBotGameModule.Start(7);

        var result = await f.Fire();

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be(PlatformErrors.BotModuleFault);
        (await f.Load()).CurrentMatch!.ChallengerSetupCommitted.Should().BeFalse();
    }
}
