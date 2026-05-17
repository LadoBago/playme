using FluentAssertions;
using PlayMe.Domain.Games.TicTacToe6x6;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Per-game rules unit tests for <see cref="TicTacToe6x6GameModule"/>. The
/// platform's move pipeline is covered by <c>SubmitMoveHandlerClockTests</c>;
/// these tests pin the module's rules directly so adding/changing rules
/// can't accidentally regress the platform tests.
///
/// Conventions:
/// - Cells are row-major; cell index = row * 6 + col, with rows 0..5.
/// - Sides are <see cref="TicTacToe6x6Sides.X"/> (first) and
///   <see cref="TicTacToe6x6Sides.O"/>.
/// - Win = at least 4 consecutive same-side marks horizontally, vertically,
///   or on either diagonal (`platform-and-games.md §2.1`). Runs of 5 or 6
///   are also wins and the detector reports the full run.
/// </summary>
public sealed class TicTacToe6x6GameModuleTests
{
    private readonly TicTacToe6x6GameModule _module = new();

    private static int Idx(int row, int col) => row * TicTacToe6x6State.Size + col;

    [Fact]
    public void Module_metadata_is_canonical()
    {
        _module.Id.Value.Should().Be("tictactoe-6x6");
        _module.ValidSides.Should().Equal(TicTacToe6x6Sides.X, TicTacToe6x6Sides.O);
        _module.FirstMoveSide.Should().Be(TicTacToe6x6Sides.X);
        _module.DefaultClockBudget.Should().Be(TimeSpan.FromMinutes(3));
        _module.OtherSide(TicTacToe6x6Sides.X).Should().Be(TicTacToe6x6Sides.O);
        _module.OtherSide(TicTacToe6x6Sides.O).Should().Be(TicTacToe6x6Sides.X);
    }

    [Fact]
    public void NewMatch_starts_with_empty_board()
    {
        var state = (TicTacToe6x6State)_module.NewMatch();

        state.Cells.Should().HaveCount(TicTacToe6x6State.CellCount);
        state.Cells.Should().AllSatisfy(c => c.Should().BeNull());
        state.LastMove.Should().BeNull();
        state.WinningLine.Should().BeNull();
    }

    [Fact]
    public void ApplyMove_places_mark_and_records_last_move()
    {
        var state = _module.NewMatch();

        var result = _module.ApplyMove(state, TicTacToe6x6Sides.X, new TicTacToe6x6Move(Idx(2, 3)));

        result.Accepted.Should().BeTrue();
        result.Ending.Should().BeNull();
        var next = (TicTacToe6x6State)result.NewState!;
        next.CellAt(Idx(2, 3)).Should().Be(TicTacToe6x6Sides.X);
        next.LastMove.Should().Be(Idx(2, 3));
    }

    [Fact]
    public void ApplyMove_rejects_negative_cell()
    {
        var state = _module.NewMatch();

        var result = _module.ApplyMove(state, TicTacToe6x6Sides.X, new TicTacToe6x6Move(-1));

        result.Accepted.Should().BeFalse();
        result.RejectKey.Should().Be(TicTacToe6x6Errors.IllegalCell);
    }

    [Fact]
    public void ApplyMove_rejects_cell_past_board_end()
    {
        var state = _module.NewMatch();

        var result = _module.ApplyMove(
            state, TicTacToe6x6Sides.X, new TicTacToe6x6Move(TicTacToe6x6State.CellCount));

        result.Accepted.Should().BeFalse();
        result.RejectKey.Should().Be(TicTacToe6x6Errors.IllegalCell);
    }

    [Fact]
    public void ApplyMove_rejects_occupied_cell()
    {
        var state = (IGameState)_module.NewMatch();
        state = _module.ApplyMove(state, TicTacToe6x6Sides.X, new TicTacToe6x6Move(Idx(0, 0))).NewState!;

        var result = _module.ApplyMove(state, TicTacToe6x6Sides.O, new TicTacToe6x6Move(Idx(0, 0)));

        result.Accepted.Should().BeFalse();
        result.RejectKey.Should().Be(TicTacToe6x6Errors.CellOccupied);
    }

    [Fact]
    public void ApplyMove_three_in_a_row_is_not_a_win()
    {
        // X plays (0,0),(0,1),(0,2); O plays (5,0),(5,1). No win — minimum
        // run is 4 (`platform-and-games.md §2.1`).
        var state = (IGameState)_module.NewMatch();
        state = _module.ApplyMove(state, TicTacToe6x6Sides.X, new TicTacToe6x6Move(Idx(0, 0))).NewState!;
        state = _module.ApplyMove(state, TicTacToe6x6Sides.O, new TicTacToe6x6Move(Idx(5, 0))).NewState!;
        state = _module.ApplyMove(state, TicTacToe6x6Sides.X, new TicTacToe6x6Move(Idx(0, 1))).NewState!;
        state = _module.ApplyMove(state, TicTacToe6x6Sides.O, new TicTacToe6x6Move(Idx(5, 1))).NewState!;
        var third = _module.ApplyMove(state, TicTacToe6x6Sides.X, new TicTacToe6x6Move(Idx(0, 2)));

        third.Accepted.Should().BeTrue();
        third.Ending.Should().BeNull();
        ((TicTacToe6x6State)third.NewState!).WinningLine.Should().BeNull();
    }

    [Fact]
    public void ApplyMove_horizontal_four_wins()
    {
        // X plays (1,0),(1,1),(1,2),(1,3); O fills (5,0),(5,1),(5,2).
        var state = (IGameState)_module.NewMatch();
        state = _module.ApplyMove(state, TicTacToe6x6Sides.X, new TicTacToe6x6Move(Idx(1, 0))).NewState!;
        state = _module.ApplyMove(state, TicTacToe6x6Sides.O, new TicTacToe6x6Move(Idx(5, 0))).NewState!;
        state = _module.ApplyMove(state, TicTacToe6x6Sides.X, new TicTacToe6x6Move(Idx(1, 1))).NewState!;
        state = _module.ApplyMove(state, TicTacToe6x6Sides.O, new TicTacToe6x6Move(Idx(5, 1))).NewState!;
        state = _module.ApplyMove(state, TicTacToe6x6Sides.X, new TicTacToe6x6Move(Idx(1, 2))).NewState!;
        state = _module.ApplyMove(state, TicTacToe6x6Sides.O, new TicTacToe6x6Move(Idx(5, 2))).NewState!;

        var winning = _module.ApplyMove(state, TicTacToe6x6Sides.X, new TicTacToe6x6Move(Idx(1, 3)));

        winning.Accepted.Should().BeTrue();
        winning.Ending.Should().BeOfType<Win>().Which.WinningSide.Should().Be(TicTacToe6x6Sides.X);
        var final = (TicTacToe6x6State)winning.NewState!;
        final.WinningLine.Should().BeEquivalentTo(new[]
        {
            new TicTacToe6x6Coordinate(1, 0),
            new TicTacToe6x6Coordinate(1, 1),
            new TicTacToe6x6Coordinate(1, 2),
            new TicTacToe6x6Coordinate(1, 3),
        }, opt => opt.WithStrictOrdering());
    }

    [Fact]
    public void ApplyMove_vertical_four_wins()
    {
        // O stacks col 2 (rows 0..3); X plays scattered cells.
        var state = (IGameState)_module.NewMatch();
        state = _module.ApplyMove(state, TicTacToe6x6Sides.X, new TicTacToe6x6Move(Idx(5, 5))).NewState!;
        state = _module.ApplyMove(state, TicTacToe6x6Sides.O, new TicTacToe6x6Move(Idx(0, 2))).NewState!;
        state = _module.ApplyMove(state, TicTacToe6x6Sides.X, new TicTacToe6x6Move(Idx(5, 4))).NewState!;
        state = _module.ApplyMove(state, TicTacToe6x6Sides.O, new TicTacToe6x6Move(Idx(1, 2))).NewState!;
        state = _module.ApplyMove(state, TicTacToe6x6Sides.X, new TicTacToe6x6Move(Idx(5, 3))).NewState!;
        state = _module.ApplyMove(state, TicTacToe6x6Sides.O, new TicTacToe6x6Move(Idx(2, 2))).NewState!;
        state = _module.ApplyMove(state, TicTacToe6x6Sides.X, new TicTacToe6x6Move(Idx(4, 0))).NewState!;

        var winning = _module.ApplyMove(state, TicTacToe6x6Sides.O, new TicTacToe6x6Move(Idx(3, 2)));

        winning.Accepted.Should().BeTrue();
        winning.Ending.Should().BeOfType<Win>().Which.WinningSide.Should().Be(TicTacToe6x6Sides.O);
        var final = (TicTacToe6x6State)winning.NewState!;
        final.WinningLine.Should().BeEquivalentTo(new[]
        {
            new TicTacToe6x6Coordinate(0, 2),
            new TicTacToe6x6Coordinate(1, 2),
            new TicTacToe6x6Coordinate(2, 2),
            new TicTacToe6x6Coordinate(3, 2),
        }, opt => opt.WithStrictOrdering());
    }

    [Fact]
    public void ApplyMove_diagonal_down_right_four_wins()
    {
        // Place three X marks on the ↘ diagonal at (0,0),(1,1),(2,2) by
        // hand, then have X complete it with (3,3). Use the state ctor
        // directly so we don't bother sequencing legal moves for the test.
        var cells = new string?[TicTacToe6x6State.CellCount];
        cells[Idx(0, 0)] = TicTacToe6x6Sides.X;
        cells[Idx(1, 1)] = TicTacToe6x6Sides.X;
        cells[Idx(2, 2)] = TicTacToe6x6Sides.X;
        var preWin = new TicTacToe6x6State(cells);

        var winning = _module.ApplyMove(preWin, TicTacToe6x6Sides.X, new TicTacToe6x6Move(Idx(3, 3)));

        winning.Accepted.Should().BeTrue();
        winning.Ending.Should().BeOfType<Win>().Which.WinningSide.Should().Be(TicTacToe6x6Sides.X);
        var final = (TicTacToe6x6State)winning.NewState!;
        final.WinningLine.Should().BeEquivalentTo(new[]
        {
            new TicTacToe6x6Coordinate(0, 0),
            new TicTacToe6x6Coordinate(1, 1),
            new TicTacToe6x6Coordinate(2, 2),
            new TicTacToe6x6Coordinate(3, 3),
        }, opt => opt.WithStrictOrdering());
    }

    [Fact]
    public void ApplyMove_diagonal_up_right_four_wins()
    {
        // O on the ↗ diagonal: (5,0),(4,1),(3,2), completed at (2,3).
        var cells = new string?[TicTacToe6x6State.CellCount];
        cells[Idx(5, 0)] = TicTacToe6x6Sides.O;
        cells[Idx(4, 1)] = TicTacToe6x6Sides.O;
        cells[Idx(3, 2)] = TicTacToe6x6Sides.O;
        var preWin = new TicTacToe6x6State(cells);

        var winning = _module.ApplyMove(preWin, TicTacToe6x6Sides.O, new TicTacToe6x6Move(Idx(2, 3)));

        winning.Accepted.Should().BeTrue();
        winning.Ending.Should().BeOfType<Win>().Which.WinningSide.Should().Be(TicTacToe6x6Sides.O);
        var final = (TicTacToe6x6State)winning.NewState!;
        final.WinningLine.Should().Contain(new TicTacToe6x6Coordinate(5, 0));
        final.WinningLine.Should().Contain(new TicTacToe6x6Coordinate(4, 1));
        final.WinningLine.Should().Contain(new TicTacToe6x6Coordinate(3, 2));
        final.WinningLine.Should().Contain(new TicTacToe6x6Coordinate(2, 3));
    }

    [Fact]
    public void ApplyMove_run_of_five_is_a_win_and_reports_full_line()
    {
        // X has four in a row at row 2 cols 1..4; closing cell at (2,0)
        // makes a run of five. The detector must report all five cells.
        var cells = new string?[TicTacToe6x6State.CellCount];
        cells[Idx(2, 1)] = TicTacToe6x6Sides.X;
        cells[Idx(2, 2)] = TicTacToe6x6Sides.X;
        cells[Idx(2, 3)] = TicTacToe6x6Sides.X;
        cells[Idx(2, 4)] = TicTacToe6x6Sides.X;
        var preWin = new TicTacToe6x6State(cells);

        var winning = _module.ApplyMove(preWin, TicTacToe6x6Sides.X, new TicTacToe6x6Move(Idx(2, 0)));

        winning.Accepted.Should().BeTrue();
        winning.Ending.Should().BeOfType<Win>().Which.WinningSide.Should().Be(TicTacToe6x6Sides.X);
        var final = (TicTacToe6x6State)winning.NewState!;
        final.WinningLine.Should().BeEquivalentTo(new[]
        {
            new TicTacToe6x6Coordinate(2, 0),
            new TicTacToe6x6Coordinate(2, 1),
            new TicTacToe6x6Coordinate(2, 2),
            new TicTacToe6x6Coordinate(2, 3),
            new TicTacToe6x6Coordinate(2, 4),
        }, opt => opt.WithStrictOrdering());
    }

    [Fact]
    public void ApplyMove_run_of_six_is_a_win_and_reports_full_row()
    {
        // X has five marks in row 3 already at cols 0..4; closing at (3,5)
        // produces a run of six. The detector must report all six cells.
        var cells = new string?[TicTacToe6x6State.CellCount];
        for (var c = 0; c < 5; c++) cells[Idx(3, c)] = TicTacToe6x6Sides.X;
        var preWin = new TicTacToe6x6State(cells);

        var winning = _module.ApplyMove(preWin, TicTacToe6x6Sides.X, new TicTacToe6x6Move(Idx(3, 5)));

        winning.Accepted.Should().BeTrue();
        winning.Ending.Should().BeOfType<Win>().Which.WinningSide.Should().Be(TicTacToe6x6Sides.X);
        var final = (TicTacToe6x6State)winning.NewState!;
        final.WinningLine.Should().HaveCount(6);
        for (var c = 0; c < 6; c++)
        {
            final.WinningLine.Should().Contain(new TicTacToe6x6Coordinate(3, c));
        }
    }

    [Fact]
    public void Draw_when_board_fills_with_no_winning_line()
    {
        // Build a 6×6 board that fills with no 4-in-a-row anywhere, then
        // play the last cell through the module so win/draw detection
        // runs. We use a "staircase" pattern: shift the X/O alternation by
        // one column every two rows so no four consecutive same-side cells
        // form on any row, column, or diagonal.
        //
        // Layout (row, col-by-col):
        //   row 0: X O X O X O
        //   row 1: O X O X O X
        //   row 2: O X O X O X
        //   row 3: X O X O X O
        //   row 4: X O X O X O
        //   row 5: O X O X O X
        //
        // Rows: never 4 same in a row (strict X O X O ... alternation).
        // Cols: every column alternates with at most 2 consecutive same.
        // Diagonals: scanning each diagonal of length ≥4 confirms no run
        //   of 4 forms (the staircase shift breaks any 4-long diagonal).
        string[][] layout =
        {
            new[] { "x","o","x","o","x","o" }, // row 0
            new[] { "o","x","o","x","o","x" }, // row 1
            new[] { "o","x","o","x","o","x" }, // row 2
            new[] { "x","o","x","o","x","o" }, // row 3
            new[] { "x","o","x","o","x","o" }, // row 4
            new[] { "o","x","o","x","o","x" }, // row 5
        };

        var cells = new string?[TicTacToe6x6State.CellCount];
        for (var row = 0; row < TicTacToe6x6State.Size; row++)
        {
            for (var col = 0; col < TicTacToe6x6State.Size; col++)
            {
                cells[Idx(row, col)] = layout[row][col] switch
                {
                    "x" => TicTacToe6x6Sides.X,
                    "o" => TicTacToe6x6Sides.O,
                    _ => null,
                };
            }
        }
        // Clear (5,5) — that's the last cell to be played.
        cells[Idx(5, 5)] = null;
        var preDraw = new TicTacToe6x6State(cells);

        var move = _module.ApplyMove(preDraw, TicTacToe6x6Sides.X, new TicTacToe6x6Move(Idx(5, 5)));

        move.Accepted.Should().BeTrue();
        move.Ending.Should().BeOfType<Draw>();
        ((TicTacToe6x6State)move.NewState!).IsFull().Should().BeTrue();
    }

    [Fact]
    public void Serialize_and_Deserialize_round_trip_preserves_state()
    {
        var state = (IGameState)_module.NewMatch();
        state = _module.ApplyMove(state, TicTacToe6x6Sides.X, new TicTacToe6x6Move(Idx(2, 2))).NewState!;
        state = _module.ApplyMove(state, TicTacToe6x6Sides.O, new TicTacToe6x6Move(Idx(3, 3))).NewState!;
        state = _module.ApplyMove(state, TicTacToe6x6Sides.X, new TicTacToe6x6Move(Idx(0, 5))).NewState!;

        var json = _module.Serialize(state);
        var restored = (TicTacToe6x6State)_module.Deserialize(json);
        var original = (TicTacToe6x6State)state;

        restored.Cells.Should().Equal(original.Cells);
        restored.LastMove.Should().Be(original.LastMove);
        restored.WinningLine.Should().BeNull();
    }

    [Fact]
    public void Deserialize_rejects_wrong_dimensions()
    {
        var bogus = """{"rows":3,"cols":3,"cells":[null,null,null,null,null,null,null,null,null]}""";

        var act = () => _module.Deserialize(bogus);

        act.Should().Throw<ArgumentException>();
    }
}
