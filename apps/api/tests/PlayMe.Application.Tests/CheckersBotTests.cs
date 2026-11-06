using System.Diagnostics;
using FluentAssertions;
using PlayMe.Domain.Games.Checkers;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// The docs/bots.md §4.1 checklist for the Checkers bot (Sprint 12 PR7):
/// legal everywhere (which, with mandatory capture and chains, is most of
/// the rules), deterministic, bounded, tactically sane, and ordered by
/// strength. Every test runs the real module: playouts commit the bot's
/// moves through <see cref="CheckersGameModule.ApplyMove"/>, chains and
/// all, so a drift between the search's board model and the rules shows
/// up here.
/// </summary>
public sealed class CheckersBotTests
{
    private static readonly CheckersGameModule Module = new();
    private static readonly BotLevel[] Levels = { BotLevel.Easy, BotLevel.Medium, BotLevel.Hard };

    /// <summary>Safety net for playouts; the draw rules end real games well before this.</summary>
    private const int MaxPlies = 600;

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

    public static IEnumerable<object[]> SearchingLevels() =>
        new[] { BotLevel.Medium, BotLevel.Hard }.Select(l => new object[] { l });

    private static CheckersCoordinate At(int row, int col) => new(row, col);

    private static string?[] Cells(params (int Row, int Col, string Piece)[] pieces)
    {
        var cells = new string?[CheckersState.CellCount];
        foreach (var (row, col, piece) in pieces)
        {
            CheckersState.IsDarkSquare(row, col).Should().BeTrue($"({row},{col}) must be a dark square");
            cells[CheckersState.IndexOf(row, col)] = piece;
        }
        return cells;
    }

    /// <summary>
    /// Hand-built position with <paramref name="side"/> to move. Round-trips
    /// through the module so <see cref="CheckersState.LegalMoves"/> is the
    /// server-computed list, exactly as the platform hands it to the bot.
    /// </summary>
    private static CheckersState State(string side, string?[] cells)
    {
        var raw = new CheckersState(
            cells, side, moveCount: 0, lastMove: null, chain: null, Array.Empty<CheckersMoveInfo>(),
            kingMovePlies: 0, Array.Empty<string>());
        return (CheckersState)Module.Deserialize(Module.Serialize(raw));
    }

    private static string Other(string side) =>
        side == CheckersSides.White ? CheckersSides.Black : CheckersSides.White;

    /// <summary>
    /// Play a full game from the opening; null level = random legal mover.
    /// White moves first. Returns null only if the ply cap is hit.
    /// </summary>
    private static Outcome? Play(BotLevel? whiteLevel, BotLevel? blackLevel, int seed)
    {
        var rng = new SeededSource(seed);
        var state = (CheckersState)Module.NewMatch(null);
        var side = CheckersSides.White;
        for (var ply = 0; ply < MaxPlies; ply++)
        {
            var level = side == CheckersSides.White ? whiteLevel : blackLevel;
            CheckersMove move;
            if (level is { } l)
            {
                move = (CheckersMove)Bot.ChooseMove(state, side, l, rng);
            }
            else
            {
                var pick = state.LegalMoves[rng.NextInt(state.LegalMoves.Count)];
                move = new CheckersMove(pick.From, pick.To);
            }

            var result = Module.ApplyMove(state, side, move);
            result.Accepted.Should().BeTrue(
                $"the bot returned {move.From}→{move.To} at {level} on ply {ply} (reject: {result.RejectKey})");
            if (result.Ending is not null) return result.Ending;
            state = (CheckersState)result.NewState!;
            if (!result.KeepTurn) side = Other(side);
        }
        return null;
    }

    [Theory]
    [MemberData(nameof(AllLevels))]
    public void Every_move_is_legal_across_random_playouts(BotLevel level)
    {
        // Hard at depth 8 is the costly one in the Debug build CI runs, and
        // legality is already exercised by every other playout in this file.
        var seeds = level == BotLevel.Hard ? 1 : 3;
        for (var seed = 0; seed < seeds; seed++)
        {
            Play(level, null, seed);
            Play(null, level, seed + 100);
        }
    }

    [Theory]
    [MemberData(nameof(AllLevels))]
    public void Same_state_and_seed_give_the_same_move(BotLevel level)
    {
        var state = (CheckersState)Module.NewMatch(null);
        var first = (CheckersMove)Bot.ChooseMove(state, CheckersSides.White, level, new SeededSource(7));
        var second = (CheckersMove)Bot.ChooseMove(state, CheckersSides.White, level, new SeededSource(7));
        first.Should().Be(second);
    }

    [Fact]
    public void Hard_stays_inside_the_budget_from_the_opening()
    {
        var state = (CheckersState)Module.NewMatch(null);
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 3; i++)
        {
            Bot.ChooseMove(state, CheckersSides.White, BotLevel.Hard, new SeededSource(i));
        }
        sw.Stop();
        // 1000 ms target on the production tier (docs/bots.md §2.2); a loose
        // laptop ceiling here, per call, catches a runaway search.
        (sw.ElapsedMilliseconds / 3).Should().BeLessThan(3000);
    }

    [Fact]
    public void Hard_stays_inside_the_budget_with_flying_kings()
    {
        // Kings fly, so an open board with several of them is where the
        // branching factor peaks.
        var state = State(CheckersSides.White, Cells(
            (0, 1, CheckersPieces.WhiteKing), (7, 6, CheckersPieces.WhiteKing), (3, 2, CheckersPieces.WhiteKing),
            (6, 3, CheckersPieces.WhiteMan), (5, 0, CheckersPieces.WhiteMan),
            (0, 7, CheckersPieces.BlackKing), (7, 0, CheckersPieces.BlackKing), (4, 5, CheckersPieces.BlackKing),
            (1, 4, CheckersPieces.BlackMan), (2, 7, CheckersPieces.BlackMan)));
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < 3; i++)
        {
            Bot.ChooseMove(state, CheckersSides.White, BotLevel.Hard, new SeededSource(i));
        }
        sw.Stop();
        (sw.ElapsedMilliseconds / 3).Should().BeLessThan(3000);
    }

    [Theory]
    [MemberData(nameof(AllLevels))]
    public void Mid_chain_continues_with_the_chaining_piece(BotLevel level)
    {
        // White (5,2) jumps (4,3) to (3,4), from where (2,5) is capturable
        // too, landing on (1,6). Commit the first jump through the module
        // so the bot sees a real mid-chain state.
        var state = State(CheckersSides.White, Cells(
            (5, 2, CheckersPieces.WhiteMan), (7, 0, CheckersPieces.WhiteMan),
            (4, 3, CheckersPieces.BlackMan), (2, 5, CheckersPieces.BlackMan), (0, 1, CheckersPieces.BlackMan)));
        var first = Module.ApplyMove(state, CheckersSides.White, new CheckersMove(At(5, 2), At(3, 4)));
        first.KeepTurn.Should().BeTrue();
        var midChain = (CheckersState)first.NewState!;

        var move = (CheckersMove)Bot.ChooseMove(midChain, CheckersSides.White, level, new SeededSource(1));
        move.Should().Be(new CheckersMove(At(3, 4), At(1, 6)));
        Module.ApplyMove(midChain, CheckersSides.White, move).Accepted.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(AllLevels))]
    public void Takes_a_free_promotion(BotLevel level)
    {
        // White (1,2) can crown on (0,1) or (0,3); the man on (6,1) is the
        // quiet alternative. Nothing black has can reach the new king.
        var state = State(CheckersSides.White, Cells(
            (1, 2, CheckersPieces.WhiteMan), (6, 1, CheckersPieces.WhiteMan),
            (0, 7, CheckersPieces.BlackMan), (7, 4, CheckersPieces.BlackMan)));
        var move = (CheckersMove)Bot.ChooseMove(state, CheckersSides.White, level, new SeededSource(1));
        move.From.Should().Be(At(1, 2));
        move.To.Row.Should().Be(0);
    }

    [Theory]
    [MemberData(nameof(SearchingLevels))]
    public void Prefers_capturing_a_king_over_a_man(BotLevel level)
    {
        // White (4,3) may jump the king on (3,2) → (2,1) or the man on
        // (3,4) → (2,5). Free choice under the rules; the search should
        // know which piece is worth more.
        var state = State(CheckersSides.White, Cells(
            (4, 3, CheckersPieces.WhiteMan), (7, 0, CheckersPieces.WhiteMan),
            (3, 2, CheckersPieces.BlackKing), (3, 4, CheckersPieces.BlackMan), (0, 5, CheckersPieces.BlackMan)));
        var move = (CheckersMove)Bot.ChooseMove(state, CheckersSides.White, level, new SeededSource(1));
        move.Should().Be(new CheckersMove(At(4, 3), At(2, 1)));
    }

    [Theory]
    [MemberData(nameof(AllLevels))]
    public void Does_not_step_into_a_capture(BotLevel level)
    {
        // White (5,2) → (4,3) would be jumped by the black man on (3,4);
        // (5,2) → (4,1) and (7,0) → (6,1) are safe. Easy must see this too
        // (docs/bots.md §2.2 "not an idiot").
        var state = State(CheckersSides.White, Cells(
            (5, 2, CheckersPieces.WhiteMan), (7, 0, CheckersPieces.WhiteMan),
            (3, 4, CheckersPieces.BlackMan), (0, 1, CheckersPieces.BlackMan)));
        for (var seed = 0; seed < 5; seed++)
        {
            var move = (CheckersMove)Bot.ChooseMove(state, CheckersSides.White, level, new SeededSource(seed));
            move.To.Should().NotBe(At(4, 3), $"seed {seed} walked into the capture");
        }
    }

    [Fact]
    public void Hard_beats_Easy_in_a_clear_majority()
    {
        var hardWins = 0;
        var easyWins = 0;
        for (var seed = 0; seed < 4; seed++)
        {
            var hardIsWhite = seed % 2 == 0;
            var outcome = hardIsWhite
                ? Play(BotLevel.Hard, BotLevel.Easy, seed)
                : Play(BotLevel.Easy, BotLevel.Hard, seed);
            if (outcome is Win w)
            {
                var hardSide = hardIsWhite ? CheckersSides.White : CheckersSides.Black;
                if (w.WinningSide == hardSide) hardWins++;
                else easyWins++;
            }
        }
        hardWins.Should().BeGreaterThan(easyWins * 2);
    }
}
