using System.Text.Json;
using FluentAssertions;
using PlayMe.Domain.Games.TicTacToe;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Per-game rules unit tests for the unified <see cref="TicTacToeGameModule"/>
/// (Sprint 9 PR1b). Parameterized across <c>boardSize</c> ∈ {3, 6, 9} so
/// every test pins the rules on all three sizes at once.
///
/// Conventions:
/// - Cells are row-major; cell index = row * N + col, with row 0 at the top.
/// - Sides are <see cref="TicTacToeSides.X"/> (first) and
///   <see cref="TicTacToeSides.O"/>.
/// - Win = a run of at least WinLength consecutive same-side cells in any
///   of four directions (horizontal, vertical, both diagonals).
/// - WinLength is derived: 3→3, 6→4, 9→5.
/// </summary>
public sealed class TicTacToeGameModuleTests
{
    private readonly TicTacToeGameModule _module = new();

    public static IEnumerable<object[]> BoardSizes() => new[]
    {
        new object[] { 3 },
        new object[] { 6 },
        new object[] { 9 },
    };

    private static JsonElement Options(int boardSize) =>
        JsonDocument.Parse($$"""{"boardSize": {{boardSize}}}""").RootElement;

    private static int Idx(int row, int col, int size) => row * size + col;

    // --- Module metadata ---

    [Fact]
    public void Module_metadata_is_canonical()
    {
        _module.Id.Value.Should().Be("tictactoe");
        _module.ValidSides.Should().Equal(TicTacToeSides.X, TicTacToeSides.O);
        _module.FirstMoveSide.Should().Be(TicTacToeSides.X);
        _module.OtherSide(TicTacToeSides.X).Should().Be(TicTacToeSides.O);
        _module.OtherSide(TicTacToeSides.O).Should().Be(TicTacToeSides.X);
        _module.DefaultClockBudget.Should().Be(TimeSpan.FromMinutes(3));
    }

    // --- ValidateOptions ---

    [Fact]
    public void ValidateOptions_rejects_null_because_module_requires_boardSize()
    {
        _module.ValidateOptions(null)
            .Should().Be(TicTacToeErrors.ConfigInvalidGameOptions);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(9)]
    public void ValidateOptions_accepts_allowed_board_size(int boardSize)
    {
        _module.ValidateOptions(Options(boardSize)).Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(-1)]
    public void ValidateOptions_rejects_other_board_sizes(int boardSize)
    {
        _module.ValidateOptions(Options(boardSize))
            .Should().Be(TicTacToeErrors.ConfigInvalidGameOptions);
    }

    [Theory]
    [InlineData("""null""")]
    [InlineData("""42""")]
    [InlineData("\"hello\"")]
    [InlineData("""[3]""")]
    [InlineData("""{}""")]
    [InlineData("""{"boardSize": "3"}""")]
    [InlineData("""{"boardSize": 3.5}""")]
    [InlineData("""{"size": 3}""")]
    public void ValidateOptions_rejects_malformed_payload(string json)
    {
        var element = JsonDocument.Parse(json).RootElement;
        _module.ValidateOptions(element)
            .Should().Be(TicTacToeErrors.ConfigInvalidGameOptions);
    }

    // --- NewMatch ---

    [Theory]
    [MemberData(nameof(BoardSizes))]
    public void NewMatch_starts_with_empty_board_and_correct_dimensions(int boardSize)
    {
        var expectedWinLength = TicTacToeGameModule.WinLengthFor(boardSize);
        var state = (TicTacToeState)_module.NewMatch(Options(boardSize));

        state.BoardSize.Should().Be(boardSize);
        state.WinLength.Should().Be(expectedWinLength);
        state.CellCount.Should().Be(boardSize * boardSize);
        state.Cells.Should().HaveCount(boardSize * boardSize);
        state.Cells.Should().AllSatisfy(c => c.Should().BeNull());
        state.LastMove.Should().BeNull();
        state.WinningLine.Should().BeNull();
    }

    // --- ApplyMove: placement + basic rejects ---

    [Theory]
    [MemberData(nameof(BoardSizes))]
    public void ApplyMove_places_x_at_chosen_cell(int boardSize)
    {
        var state = _module.NewMatch(Options(boardSize));
        var cell = Idx(0, 0, boardSize);

        var result = _module.ApplyMove(state, TicTacToeSides.X, new TicTacToeMove(cell));

        result.Accepted.Should().BeTrue();
        result.Ending.Should().BeNull();
        var next = (TicTacToeState)result.NewState!;
        next.CellAt(cell).Should().Be(TicTacToeSides.X);
        next.LastMove.Should().Be(cell);
    }

    [Theory]
    [MemberData(nameof(BoardSizes))]
    public void ApplyMove_rejects_negative_cell(int boardSize)
    {
        var state = _module.NewMatch(Options(boardSize));

        var result = _module.ApplyMove(state, TicTacToeSides.X, new TicTacToeMove(-1));

        result.Accepted.Should().BeFalse();
        result.RejectKey.Should().Be(TicTacToeErrors.IllegalCell);
    }

    [Theory]
    [MemberData(nameof(BoardSizes))]
    public void ApplyMove_rejects_cell_past_last_index(int boardSize)
    {
        var state = (TicTacToeState)_module.NewMatch(Options(boardSize));

        var result = _module.ApplyMove(
            state, TicTacToeSides.X, new TicTacToeMove(state.CellCount));

        result.Accepted.Should().BeFalse();
        result.RejectKey.Should().Be(TicTacToeErrors.IllegalCell);
    }

    [Theory]
    [MemberData(nameof(BoardSizes))]
    public void ApplyMove_rejects_occupied_cell(int boardSize)
    {
        IGameState state = _module.NewMatch(Options(boardSize));
        var cell = Idx(0, 0, boardSize);
        state = _module.ApplyMove(state, TicTacToeSides.X, new TicTacToeMove(cell)).NewState!;

        var result = _module.ApplyMove(state, TicTacToeSides.O, new TicTacToeMove(cell));

        result.Accepted.Should().BeFalse();
        result.RejectKey.Should().Be(TicTacToeErrors.CellOccupied);
    }

    // --- ApplyMove: wins on exactly winLength run, each direction ---

    [Theory]
    [MemberData(nameof(BoardSizes))]
    public void Horizontal_run_of_exactly_winLength_wins(int boardSize)
    {
        var winLength = TicTacToeGameModule.WinLengthFor(boardSize);
        var preWin = SeedBoard(boardSize, (row: 0, col: 0, dr: 0, dc: 1, length: winLength - 1));
        var lastCell = Idx(0, winLength - 1, boardSize);

        var result = _module.ApplyMove(
            preWin, TicTacToeSides.X, new TicTacToeMove(lastCell));

        AssertWinningLine(result, winLength, expectedRows: Enumerable.Range(0, winLength).Select(_ => 0).ToArray(),
            expectedCols: Enumerable.Range(0, winLength).ToArray());
    }

    [Theory]
    [MemberData(nameof(BoardSizes))]
    public void Vertical_run_of_exactly_winLength_wins(int boardSize)
    {
        var winLength = TicTacToeGameModule.WinLengthFor(boardSize);
        var preWin = SeedBoard(boardSize, (row: 0, col: 1, dr: 1, dc: 0, length: winLength - 1));
        var lastCell = Idx(winLength - 1, 1, boardSize);

        var result = _module.ApplyMove(
            preWin, TicTacToeSides.X, new TicTacToeMove(lastCell));

        AssertWinningLine(result, winLength,
            expectedRows: Enumerable.Range(0, winLength).ToArray(),
            expectedCols: Enumerable.Range(0, winLength).Select(_ => 1).ToArray());
    }

    [Theory]
    [MemberData(nameof(BoardSizes))]
    public void Diagonal_down_run_of_exactly_winLength_wins(int boardSize)
    {
        var winLength = TicTacToeGameModule.WinLengthFor(boardSize);
        var preWin = SeedBoard(boardSize, (row: 0, col: 0, dr: 1, dc: 1, length: winLength - 1));
        var lastCell = Idx(winLength - 1, winLength - 1, boardSize);

        var result = _module.ApplyMove(
            preWin, TicTacToeSides.X, new TicTacToeMove(lastCell));

        AssertWinningLine(result, winLength,
            expectedRows: Enumerable.Range(0, winLength).ToArray(),
            expectedCols: Enumerable.Range(0, winLength).ToArray());
    }

    [Theory]
    [MemberData(nameof(BoardSizes))]
    public void Diagonal_up_run_of_exactly_winLength_wins(int boardSize)
    {
        var winLength = TicTacToeGameModule.WinLengthFor(boardSize);
        // Start at bottom-left of the would-be run; walk up-right (-1, +1).
        var preWin = SeedBoard(boardSize, (row: winLength - 1, col: 0, dr: -1, dc: 1, length: winLength - 1));
        var lastCell = Idx(0, winLength - 1, boardSize);

        var result = _module.ApplyMove(
            preWin, TicTacToeSides.X, new TicTacToeMove(lastCell));

        AssertWinningLine(result, winLength,
            expectedRows: Enumerable.Range(0, winLength).Select(i => winLength - 1 - i).ToArray(),
            expectedCols: Enumerable.Range(0, winLength).ToArray());
    }

    // --- ApplyMove: longer-than-winLength runs report the full extent ---

    [Theory]
    [InlineData(6)]
    [InlineData(9)]
    public void Run_longer_than_winLength_wins_with_full_run_in_line(int boardSize)
    {
        // Seed an N-long horizontal run by completing the move that closes
        // a length-N segment (where N == boardSize > winLength). The
        // winning line reports the entire run, not just `winLength` cells.
        var preWin = SeedBoard(boardSize, (row: 0, col: 0, dr: 0, dc: 1, length: boardSize - 1));
        var lastCell = Idx(0, boardSize - 1, boardSize);

        var result = _module.ApplyMove(
            preWin, TicTacToeSides.X, new TicTacToeMove(lastCell));

        AssertWinningLine(result, expectedLength: boardSize,
            expectedRows: Enumerable.Range(0, boardSize).Select(_ => 0).ToArray(),
            expectedCols: Enumerable.Range(0, boardSize).ToArray());
    }

    // --- ApplyMove: draw on full board with no winning run ---

    [Fact]
    public void Full_3x3_board_with_no_run_ends_in_draw()
    {
        // Classic anti-symmetric pattern that fills 3×3 with no line:
        //   X O X
        //   X O X
        //   O X O
        // Pre-load 8 cells, then play the final X at (1,2).
        var preDraw = new TicTacToeState(
            boardSize: 3,
            winLength: 3,
            cells: new string?[]
            {
                TicTacToeSides.X, TicTacToeSides.O, TicTacToeSides.X,
                TicTacToeSides.X, TicTacToeSides.O, null,
                TicTacToeSides.O, TicTacToeSides.X, TicTacToeSides.O,
            });

        var result = _module.ApplyMove(preDraw, TicTacToeSides.X, new TicTacToeMove(Idx(1, 2, 3)));

        result.Accepted.Should().BeTrue();
        result.Ending.Should().BeOfType<Draw>();
        ((TicTacToeState)result.NewState!).IsFull().Should().BeTrue();
        ((TicTacToeState)result.NewState!).WinningLine.Should().BeNull();
    }

    // --- Serialize / Deserialize round-trip ---

    [Theory]
    [MemberData(nameof(BoardSizes))]
    public void Serialize_then_Deserialize_round_trips_empty_state(int boardSize)
    {
        var original = (TicTacToeState)_module.NewMatch(Options(boardSize));

        var json = _module.Serialize(original);
        var roundTripped = (TicTacToeState)_module.Deserialize(json);

        roundTripped.BoardSize.Should().Be(boardSize);
        roundTripped.WinLength.Should().Be(TicTacToeGameModule.WinLengthFor(boardSize));
        roundTripped.Cells.Should().Equal(original.Cells);
        roundTripped.LastMove.Should().BeNull();
        roundTripped.WinningLine.Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(BoardSizes))]
    public void Serialize_then_Deserialize_round_trips_state_with_moves(int boardSize)
    {
        IGameState state = _module.NewMatch(Options(boardSize));
        state = _module.ApplyMove(state, TicTacToeSides.X, new TicTacToeMove(Idx(0, 0, boardSize))).NewState!;
        state = _module.ApplyMove(state, TicTacToeSides.O, new TicTacToeMove(Idx(1, 1, boardSize))).NewState!;
        var before = (TicTacToeState)state;

        var json = _module.Serialize(before);
        var after = (TicTacToeState)_module.Deserialize(json);

        after.BoardSize.Should().Be(before.BoardSize);
        after.WinLength.Should().Be(before.WinLength);
        after.Cells.Should().Equal(before.Cells);
        after.LastMove.Should().Be(before.LastMove);
    }

    [Fact]
    public void Deserialize_rejects_shape_with_unknown_board_size()
    {
        // 4×4 isn't an allowed boardSize — bytes that round-trip JSON
        // cleanly but violate the rules layer must be rejected.
        var json = """{"rows":4,"cols":4,"winLength":3,"cells":[null,null,null,null,null,null,null,null,null,null,null,null,null,null,null,null]}""";

        var act = () => _module.Deserialize(json);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Deserialize_rejects_winLength_inconsistent_with_boardSize()
    {
        // boardSize=3 must have winLength=3; 5 is the 9×9 mapping.
        var json = """{"rows":3,"cols":3,"winLength":5,"cells":[null,null,null,null,null,null,null,null,null]}""";

        var act = () => _module.Deserialize(json);

        act.Should().Throw<ArgumentException>();
    }

    // --- Helpers ---

    /// <summary>
    /// Build a <see cref="TicTacToeState"/> for <paramref name="boardSize"/>
    /// with one run of X cells placed starting at (<c>row</c>, <c>col</c>)
    /// stepping by (<c>dr</c>, <c>dc</c>) for <c>length</c> cells.
    /// </summary>
    private static TicTacToeState SeedBoard(
        int boardSize,
        (int row, int col, int dr, int dc, int length) run)
    {
        var cells = new string?[boardSize * boardSize];
        for (var i = 0; i < run.length; i++)
        {
            var r = run.row + run.dr * i;
            var c = run.col + run.dc * i;
            cells[Idx(r, c, boardSize)] = TicTacToeSides.X;
        }
        return new TicTacToeState(boardSize, TicTacToeGameModule.WinLengthFor(boardSize), cells);
    }

    private static void AssertWinningLine(
        MoveResult result, int expectedLength, int[] expectedRows, int[] expectedCols)
    {
        result.Accepted.Should().BeTrue();
        result.Ending.Should().BeOfType<Win>().Which.WinningSide.Should().Be(TicTacToeSides.X);
        var final = (TicTacToeState)result.NewState!;
        final.WinningLine.Should().NotBeNull();
        final.WinningLine!.Should().HaveCount(expectedLength);
        for (var i = 0; i < expectedLength; i++)
        {
            final.WinningLine![i].Row.Should().Be(expectedRows[i]);
            final.WinningLine![i].Col.Should().Be(expectedCols[i]);
        }
    }
}
