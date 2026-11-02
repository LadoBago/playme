using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using PlayMe.Domain.Games.TicTacToe;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// The docs/bots.md §4.1 checklist for the Tic-Tac-Toe bot (Sprint 12
/// PR5): legal everywhere, deterministic, bounded, tactically sane, and
/// ordered by strength. Every test runs the real module so a drift
/// between the bot's own win probe and the rules shows up here.
/// </summary>
public sealed class TicTacToeBotTests
{
    private static readonly TicTacToeGameModule Module = new();
    private static readonly int[] Sizes = { 3, 6, 9 };
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

    public static IEnumerable<object[]> SizesAndLevels() =>
        from size in Sizes
        from level in Levels
        select new object[] { size, level };

    private static TicTacToeState Fresh(int size) =>
        (TicTacToeState)Module.NewMatch(JsonDocument.Parse($$"""{"boardSize": {{size}}}""").RootElement);

    private static TicTacToeState Board(int size, string picture)
    {
        // picture: row-major characters x / o / . — whitespace ignored.
        var cells = picture.Where(ch => !char.IsWhiteSpace(ch))
            .Select(ch => ch switch { 'x' => TicTacToeSides.X, 'o' => TicTacToeSides.O, _ => (string?)null })
            .ToList();
        return new TicTacToeState(size, TicTacToeGameModule.WinLengthFor(size), cells);
    }

    private static string Other(string side) => side == TicTacToeSides.X ? TicTacToeSides.O : TicTacToeSides.X;

    /// <summary>
    /// Play a full game between two levels (or a random mover when a level
    /// is null) and return the outcome. X moves first.
    /// </summary>
    private static Outcome? Play(int size, BotLevel? xLevel, BotLevel? oLevel, int seed)
    {
        var rng = new SeededSource(seed);
        var state = Fresh(size);
        var side = TicTacToeSides.X;
        while (true)
        {
            var level = side == TicTacToeSides.X ? xLevel : oLevel;
            TicTacToeMove move;
            if (level is { } l)
            {
                move = (TicTacToeMove)Bot.ChooseMove(state, side, l, rng);
            }
            else
            {
                var empties = Enumerable.Range(0, state.CellCount).Where(state.IsCellEmpty).ToList();
                move = new TicTacToeMove(empties[rng.NextInt(empties.Count)]);
            }

            var result = Module.ApplyMove(state, side, move);
            result.Accepted.Should().BeTrue($"the bot returned cell {move.Cell} on a {size}×{size} board at {level}");
            if (result.Ending is not null) return result.Ending;
            state = (TicTacToeState)result.NewState!;
            side = Other(side);
        }
    }

    [Theory]
    [MemberData(nameof(SizesAndLevels))]
    public void Every_move_is_legal_across_random_playouts(int size, BotLevel level)
    {
        // Bot vs random mover, several seeds — ApplyMove inside Play asserts legality.
        for (var seed = 0; seed < 6; seed++)
        {
            Play(size, level, null, seed);
            Play(size, null, level, seed + 100);
        }
    }

    [Theory]
    [MemberData(nameof(SizesAndLevels))]
    public void Same_state_and_seed_give_the_same_move(int size, BotLevel level)
    {
        var state = Fresh(size);
        var first = (TicTacToeMove)Bot.ChooseMove(state, TicTacToeSides.X, level, new SeededSource(7));
        var second = (TicTacToeMove)Bot.ChooseMove(state, TicTacToeSides.X, level, new SeededSource(7));
        first.Should().Be(second);
    }

    [Fact]
    public void Hard_on_9x9_stays_inside_the_budget_on_a_mid_game_board()
    {
        // A scattered mid-game position: enough marks to make the
        // neighbourhood wide, no immediate win or block to short-circuit.
        var board = Board(9, """
            . . . . . . . . .
            . x . . o . . . .
            . . o x . . x . .
            . . . x o . . . .
            . . o . . x . . .
            . . . . o . . o .
            . x . . . . . . .
            . . . . . . x . .
            . . . . . . . . .
            """);
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 3; i++)
        {
            Bot.ChooseMove(board, TicTacToeSides.O, BotLevel.Hard, new SeededSource(i));
        }
        sw.Stop();
        // 1000 ms target on the production tier (docs/bots.md §2.2); a loose
        // laptop ceiling here, per call, catches a runaway search.
        (sw.ElapsedMilliseconds / 3).Should().BeLessThan(3000);
    }

    [Theory]
    [InlineData(BotLevel.Easy)]
    [InlineData(BotLevel.Medium)]
    [InlineData(BotLevel.Hard)]
    public void Takes_an_immediate_win(BotLevel level)
    {
        var board = Board(3, """
            x x .
            o o .
            . . .
            """);
        var move = (TicTacToeMove)Bot.ChooseMove(board, TicTacToeSides.X, level, new SeededSource(1));
        move.Cell.Should().Be(2);
    }

    [Theory]
    [InlineData(BotLevel.Easy)]
    [InlineData(BotLevel.Medium)]
    [InlineData(BotLevel.Hard)]
    public void Blocks_an_immediate_loss(BotLevel level)
    {
        var board = Board(3, """
            x x .
            o . .
            . . .
            """);
        var move = (TicTacToeMove)Bot.ChooseMove(board, TicTacToeSides.O, level, new SeededSource(1));
        move.Cell.Should().Be(2);
    }

    [Theory]
    [InlineData(BotLevel.Easy)]
    [InlineData(BotLevel.Medium)]
    [InlineData(BotLevel.Hard)]
    public void Blocks_a_four_in_a_row_threat_on_9x9(BotLevel level)
    {
        var board = Board(9, """
            . . . . . . . . .
            . . . . . . . . .
            . . x x x x . . .
            . . o o o . . . .
            . . . . . . . . .
            . . . . . . . . .
            . . . . . . . . .
            . . . . . . . . .
            . . . . . . . . .
            """);
        var move = (TicTacToeMove)Bot.ChooseMove(board, TicTacToeSides.O, level, new SeededSource(1));
        move.Cell.Should().BeOneOf(2 * 9 + 1, 2 * 9 + 6);
    }

    [Fact]
    public void Hard_never_loses_3x3_against_a_random_mover()
    {
        for (var seed = 0; seed < 30; seed++)
        {
            if (Play(3, null, BotLevel.Hard, seed) is Win lostAsO)
            {
                lostAsO.WinningSide.Should().Be(TicTacToeSides.O, $"seed {seed}: Hard as O must not lose");
            }
            if (Play(3, BotLevel.Hard, null, seed + 1000) is Win lostAsX)
            {
                lostAsX.WinningSide.Should().Be(TicTacToeSides.X, $"seed {seed}: Hard as X must not lose");
            }
        }
    }

    [Fact]
    public void Hard_beats_Easy_on_6x6_in_a_clear_majority()
    {
        var hardWins = 0;
        var easyWins = 0;
        for (var seed = 0; seed < 12; seed++)
        {
            // Alternate colours so first-move advantage cancels out.
            var hardIsX = seed % 2 == 0;
            var outcome = hardIsX
                ? Play(6, BotLevel.Hard, BotLevel.Easy, seed)
                : Play(6, BotLevel.Easy, BotLevel.Hard, seed);
            if (outcome is Win w)
            {
                var hardSide = hardIsX ? TicTacToeSides.X : TicTacToeSides.O;
                if (w.WinningSide == hardSide) hardWins++;
                else easyWins++;
            }
        }
        hardWins.Should().BeGreaterThan(easyWins * 2);
    }

    [Fact]
    public void Opening_move_on_a_big_board_is_the_centre()
    {
        var move = (TicTacToeMove)Bot.ChooseMove(Fresh(9), TicTacToeSides.X, BotLevel.Hard, new SeededSource(1));
        move.Cell.Should().Be(4 * 9 + 4);
    }
}
