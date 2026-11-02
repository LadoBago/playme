using System.Diagnostics;
using FluentAssertions;
using PlayMe.Domain.Games.Connect4;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// The docs/bots.md §4.1 checklist for the Connect 4 bot (Sprint 12 PR6):
/// legal everywhere, deterministic, bounded, tactically sane, and ordered
/// by strength. Every test runs the real module so a drift between the
/// bot's own win probe and the rules shows up here.
/// </summary>
public sealed class Connect4BotTests
{
    private static readonly Connect4GameModule Module = new();
    private static readonly BotLevel[] Levels = { BotLevel.Easy, BotLevel.Medium, BotLevel.Hard };

    // Through the capability interface on purpose: the platform only ever
    // sees IBotPlayer, so the tests should too. CA1859 would rather we used
    // the concrete type; the cast is the point here.
#pragma warning disable CA1859
    private static IBotPlayer Bot => Module;
#pragma warning restore CA1859

    private sealed class SeededSource : IRandomSource
    {
        private readonly Random _random;

        public SeededSource(int seed)
        {
            _random = new Random(seed);
        }

        public int NextInt(int maxExclusive) => _random.Next(maxExclusive);
    }

    public static IEnumerable<object[]> AllLevels() => Levels.Select(l => new object[] { l });

    private static Connect4State Board(string picture)
    {
        // picture: six rows top to bottom, characters r / y / . — whitespace ignored.
        var cells = picture.Where(ch => !char.IsWhiteSpace(ch))
            .Select(ch => ch switch { 'r' => Connect4Sides.Red, 'y' => Connect4Sides.Yellow, _ => (string?)null })
            .ToList();
        return new Connect4State(cells);
    }

    private static string Other(string side) => side == Connect4Sides.Red ? Connect4Sides.Yellow : Connect4Sides.Red;

    /// <summary>Play a full game; null level = random legal mover. Red moves first.</summary>
    private static Outcome Play(BotLevel? redLevel, BotLevel? yellowLevel, int seed)
    {
        var rng = new SeededSource(seed);
        var state = new Connect4State();
        var side = Connect4Sides.Red;
        while (true)
        {
            var level = side == Connect4Sides.Red ? redLevel : yellowLevel;
            Connect4Move move;
            if (level is { } l)
            {
                move = (Connect4Move)Bot.ChooseMove(state, side, l, rng);
            }
            else
            {
                var open = Enumerable.Range(0, Connect4State.Cols).Where(c => state.LandingRowFor(c) is not null).ToList();
                move = new Connect4Move(open[rng.NextInt(open.Count)]);
            }

            var result = Module.ApplyMove(state, side, move);
            result.Accepted.Should().BeTrue($"the bot returned column {move.Column} at {level}");
            if (result.Ending is not null) return result.Ending;
            state = (Connect4State)result.NewState!;
            side = Other(side);
        }
    }

    [Theory]
    [MemberData(nameof(AllLevels))]
    public void Every_move_is_legal_across_random_playouts(BotLevel level)
    {
        // Four seeds per colour: Hard at depth 8 is the costly one in the
        // Debug build CI runs, and legality is already exercised by every
        // other playout in this file.
        for (var seed = 0; seed < 4; seed++)
        {
            Play(level, null, seed);
            Play(null, level, seed + 100);
        }
    }

    [Theory]
    [MemberData(nameof(AllLevels))]
    public void Same_state_and_seed_give_the_same_move(BotLevel level)
    {
        var state = Board("""
            . . . . . . .
            . . . . . . .
            . . . . . . .
            . . . y . . .
            . . r r y . .
            . y r y r . .
            """);
        var first = (Connect4Move)Bot.ChooseMove(state, Connect4Sides.Red, level, new SeededSource(7));
        var second = (Connect4Move)Bot.ChooseMove(state, Connect4Sides.Red, level, new SeededSource(7));
        first.Should().Be(second);
    }

    [Fact]
    public void Hard_stays_inside_the_budget_from_an_open_position()
    {
        // Early position: the tree is at its widest (all seven columns open
        // for the full depth), with no immediate win or block to short-circuit.
        var board = Board("""
            . . . . . . .
            . . . . . . .
            . . . . . . .
            . . . . . . .
            . . . y . . .
            . . r r y . .
            """);
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 3; i++)
        {
            Bot.ChooseMove(board, Connect4Sides.Red, BotLevel.Hard, new SeededSource(i));
        }
        sw.Stop();
        // 1000 ms target on the production tier (docs/bots.md §2.2); a loose
        // laptop ceiling here, per call, catches a runaway search.
        (sw.ElapsedMilliseconds / 3).Should().BeLessThan(3000);
    }

    [Theory]
    [MemberData(nameof(AllLevels))]
    public void Takes_an_immediate_win(BotLevel level)
    {
        var board = Board("""
            . . . . . . .
            . . . . . . .
            . . . . . . .
            . . . . . . .
            . . . y y . .
            . r r r y . .
            """);
        var move = (Connect4Move)Bot.ChooseMove(board, Connect4Sides.Red, level, new SeededSource(1));
        move.Column.Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(AllLevels))]
    public void Blocks_a_vertical_threat(BotLevel level)
    {
        var board = Board("""
            . . . . . . .
            . . . . . . .
            . . . . . . .
            . . . r . . .
            . . . r y . .
            . . . r y . .
            """);
        var move = (Connect4Move)Bot.ChooseMove(board, Connect4Sides.Yellow, level, new SeededSource(1));
        move.Column.Should().Be(3);
    }

    [Theory]
    [MemberData(nameof(AllLevels))]
    public void Blocks_a_horizontal_threat_only_where_gravity_allows(BotLevel level)
    {
        // Red threatens columns 0 and 4 on the bottom row; column 4 is the
        // only one a disc can actually land on the bottom row, and column
        // 0 too — both are legal blocks; any other column loses.
        var board = Board("""
            . . . . . . .
            . . . . . . .
            . . . . . . .
            . . . . . . .
            . . y y . . .
            . r r r . . .
            """);
        var move = (Connect4Move)Bot.ChooseMove(board, Connect4Sides.Yellow, level, new SeededSource(1));
        move.Column.Should().BeOneOf(0, 4);
    }

    [Fact]
    public void Hard_sees_a_two_move_fork_and_avoids_handing_it_over()
    {
        // Red to move. Dropping in column 3 would land on row 2 and give
        // yellow nothing; the real test is that Hard does not pick a column
        // that lets yellow complete a diagonal next turn. Assert only on
        // the outcome of a Hard-vs-Hard playout from here: with correct
        // search neither side blunders into an immediate loss, so the
        // game lasts well past the next few plies.
        var board = Board("""
            . . . . . . .
            . . . . . . .
            . . . . . . .
            . . . y . . .
            . . y r r . .
            . y r r y . .
            """);
        var state = board;
        var side = Connect4Sides.Red;
        var plies = 0;
        while (plies < 6)
        {
            var move = (Connect4Move)Bot.ChooseMove(state, side, BotLevel.Hard, new SeededSource(plies));
            var result = Module.ApplyMove(state, side, move);
            result.Accepted.Should().BeTrue();
            result.Ending.Should().BeNull($"no one should lose within {plies + 1} plies of Hard-vs-Hard from this position");
            state = (Connect4State)result.NewState!;
            side = Other(side);
            plies++;
        }
    }

    [Fact]
    public void Hard_beats_Easy_in_a_clear_majority()
    {
        var hardWins = 0;
        var easyWins = 0;
        for (var seed = 0; seed < 8; seed++)
        {
            var hardIsRed = seed % 2 == 0;
            var outcome = hardIsRed
                ? Play(BotLevel.Hard, BotLevel.Easy, seed)
                : Play(BotLevel.Easy, BotLevel.Hard, seed);
            if (outcome is Win w)
            {
                var hardSide = hardIsRed ? Connect4Sides.Red : Connect4Sides.Yellow;
                if (w.WinningSide == hardSide) hardWins++;
                else easyWins++;
            }
        }
        hardWins.Should().BeGreaterThan(easyWins * 2);
    }

    [Fact]
    public void Opening_move_is_the_centre_column()
    {
        var move = (Connect4Move)Bot.ChooseMove(new Connect4State(), Connect4Sides.Red, BotLevel.Hard, new SeededSource(1));
        move.Column.Should().Be(3);
    }
}
