using FluentAssertions;
using PlayMe.Domain.Games.TicTacToe9x9;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Per-game rules unit tests for <see cref="TicTacToe9x9GameModule"/>. The
/// platform's move pipeline is covered by <c>SubmitMoveHandlerClockTests</c>;
/// these tests pin the module's rules directly so adding/changing rules
/// can't accidentally regress the platform tests.
///
/// Conventions:
/// - Cells are row-major; cell index = row * 9 + col, with row 0 at the top.
/// - Sides are <see cref="TicTacToe9x9Sides.X"/> (first) and
///   <see cref="TicTacToe9x9Sides.O"/>.
/// - Win = a run of at least 5 consecutive same-side cells in any direction.
/// </summary>
public sealed class TicTacToe9x9GameModuleTests
{
    private readonly TicTacToe9x9GameModule _module = new();

    private static int Idx(int row, int col) => row * TicTacToe9x9State.Size + col;

    [Fact]
    public void NewMatch_starts_with_empty_board_and_x_first()
    {
        var state = (TicTacToe9x9State)_module.NewMatch(null);

        state.Cells.Should().HaveCount(TicTacToe9x9State.CellCount);
        state.Cells.Should().AllSatisfy(c => c.Should().BeNull());
        state.LastMove.Should().BeNull();
        state.WinningLine.Should().BeNull();
        _module.FirstMoveSide.Should().Be(TicTacToe9x9Sides.X);
    }

    [Fact]
    public void Module_metadata_is_canonical()
    {
        _module.Id.Value.Should().Be("tictactoe-9x9");
        _module.ValidSides.Should().Equal(TicTacToe9x9Sides.X, TicTacToe9x9Sides.O);
        _module.DefaultClockBudget.Should().Be(TimeSpan.FromMinutes(10));
        _module.OtherSide(TicTacToe9x9Sides.X).Should().Be(TicTacToe9x9Sides.O);
        _module.OtherSide(TicTacToe9x9Sides.O).Should().Be(TicTacToe9x9Sides.X);
    }

    [Fact]
    public void ApplyMove_places_x_at_chosen_cell()
    {
        var state = _module.NewMatch(null);

        var result = _module.ApplyMove(state, TicTacToe9x9Sides.X, new TicTacToe9x9Move(Idx(4, 4)));

        result.Accepted.Should().BeTrue();
        result.Ending.Should().BeNull();
        var next = (TicTacToe9x9State)result.NewState!;
        next.CellAt(Idx(4, 4)).Should().Be(TicTacToe9x9Sides.X);
        next.LastMove.Should().Be(Idx(4, 4));
        // All other cells stay empty.
        for (var i = 0; i < TicTacToe9x9State.CellCount; i++)
        {
            if (i == Idx(4, 4)) continue;
            next.CellAt(i).Should().BeNull();
        }
    }

    [Fact]
    public void ApplyMove_rejects_negative_cell()
    {
        var state = _module.NewMatch(null);

        var result = _module.ApplyMove(state, TicTacToe9x9Sides.X, new TicTacToe9x9Move(-1));

        result.Accepted.Should().BeFalse();
        result.RejectKey.Should().Be(TicTacToe9x9Errors.IllegalCell);
    }

    [Fact]
    public void ApplyMove_rejects_cell_past_last_index()
    {
        var state = _module.NewMatch(null);

        var result = _module.ApplyMove(
            state, TicTacToe9x9Sides.X, new TicTacToe9x9Move(TicTacToe9x9State.CellCount));

        result.Accepted.Should().BeFalse();
        result.RejectKey.Should().Be(TicTacToe9x9Errors.IllegalCell);
    }

    [Fact]
    public void ApplyMove_rejects_occupied_cell()
    {
        IGameState state = _module.NewMatch(null);
        state = _module.ApplyMove(state, TicTacToe9x9Sides.X, new TicTacToe9x9Move(Idx(0, 0))).NewState!;

        var result = _module.ApplyMove(state, TicTacToe9x9Sides.O, new TicTacToe9x9Move(Idx(0, 0)));

        result.Accepted.Should().BeFalse();
        result.RejectKey.Should().Be(TicTacToe9x9Errors.CellOccupied);
    }

    [Fact]
    public void Horizontal_run_of_exactly_five_wins()
    {
        // Seed X at (3,0)..(3,3) directly so we only need to play the win
        // move. The rules check needs to fire on the just-played cell — the
        // run finishes when X plays (3,4).
        var cells = new string?[TicTacToe9x9State.CellCount];
        cells[Idx(3, 0)] = TicTacToe9x9Sides.X;
        cells[Idx(3, 1)] = TicTacToe9x9Sides.X;
        cells[Idx(3, 2)] = TicTacToe9x9Sides.X;
        cells[Idx(3, 3)] = TicTacToe9x9Sides.X;
        var preWin = new TicTacToe9x9State(cells);

        var result = _module.ApplyMove(preWin, TicTacToe9x9Sides.X, new TicTacToe9x9Move(Idx(3, 4)));

        result.Accepted.Should().BeTrue();
        result.Ending.Should().BeOfType<Win>().Which.WinningSide.Should().Be(TicTacToe9x9Sides.X);
        var final = (TicTacToe9x9State)result.NewState!;
        final.WinningLine.Should().BeEquivalentTo(new[]
        {
            new TicTacToe9x9Coordinate(3, 0),
            new TicTacToe9x9Coordinate(3, 1),
            new TicTacToe9x9Coordinate(3, 2),
            new TicTacToe9x9Coordinate(3, 3),
            new TicTacToe9x9Coordinate(3, 4),
        }, opt => opt.WithStrictOrdering());
    }

    [Fact]
    public void Vertical_run_of_five_wins()
    {
        var cells = new string?[TicTacToe9x9State.CellCount];
        cells[Idx(0, 2)] = TicTacToe9x9Sides.O;
        cells[Idx(1, 2)] = TicTacToe9x9Sides.O;
        cells[Idx(2, 2)] = TicTacToe9x9Sides.O;
        cells[Idx(3, 2)] = TicTacToe9x9Sides.O;
        var preWin = new TicTacToe9x9State(cells);

        var result = _module.ApplyMove(preWin, TicTacToe9x9Sides.O, new TicTacToe9x9Move(Idx(4, 2)));

        result.Accepted.Should().BeTrue();
        result.Ending.Should().BeOfType<Win>().Which.WinningSide.Should().Be(TicTacToe9x9Sides.O);
        var final = (TicTacToe9x9State)result.NewState!;
        final.WinningLine!.Should().HaveCount(5);
        final.WinningLine.Should().Contain(new TicTacToe9x9Coordinate(0, 2));
        final.WinningLine.Should().Contain(new TicTacToe9x9Coordinate(4, 2));
    }

    [Fact]
    public void Diagonal_down_right_run_of_five_wins()
    {
        // ↘ diagonal at (0,0),(1,1),(2,2),(3,3),(4,4).
        var cells = new string?[TicTacToe9x9State.CellCount];
        cells[Idx(0, 0)] = TicTacToe9x9Sides.X;
        cells[Idx(1, 1)] = TicTacToe9x9Sides.X;
        cells[Idx(2, 2)] = TicTacToe9x9Sides.X;
        cells[Idx(3, 3)] = TicTacToe9x9Sides.X;
        var preWin = new TicTacToe9x9State(cells);

        var result = _module.ApplyMove(preWin, TicTacToe9x9Sides.X, new TicTacToe9x9Move(Idx(4, 4)));

        result.Accepted.Should().BeTrue();
        result.Ending.Should().BeOfType<Win>().Which.WinningSide.Should().Be(TicTacToe9x9Sides.X);
        var final = (TicTacToe9x9State)result.NewState!;
        final.WinningLine.Should().BeEquivalentTo(new[]
        {
            new TicTacToe9x9Coordinate(0, 0),
            new TicTacToe9x9Coordinate(1, 1),
            new TicTacToe9x9Coordinate(2, 2),
            new TicTacToe9x9Coordinate(3, 3),
            new TicTacToe9x9Coordinate(4, 4),
        }, opt => opt.WithStrictOrdering());
    }

    [Fact]
    public void Diagonal_up_right_run_of_five_wins()
    {
        // ↗ diagonal at (8,0),(7,1),(6,2),(5,3),(4,4).
        var cells = new string?[TicTacToe9x9State.CellCount];
        cells[Idx(8, 0)] = TicTacToe9x9Sides.O;
        cells[Idx(7, 1)] = TicTacToe9x9Sides.O;
        cells[Idx(6, 2)] = TicTacToe9x9Sides.O;
        cells[Idx(5, 3)] = TicTacToe9x9Sides.O;
        var preWin = new TicTacToe9x9State(cells);

        var result = _module.ApplyMove(preWin, TicTacToe9x9Sides.O, new TicTacToe9x9Move(Idx(4, 4)));

        result.Accepted.Should().BeTrue();
        result.Ending.Should().BeOfType<Win>().Which.WinningSide.Should().Be(TicTacToe9x9Sides.O);
        var final = (TicTacToe9x9State)result.NewState!;
        final.WinningLine!.Should().HaveCount(5);
        final.WinningLine.Should().Contain(new TicTacToe9x9Coordinate(8, 0));
        final.WinningLine.Should().Contain(new TicTacToe9x9Coordinate(4, 4));
    }

    [Fact]
    public void Run_of_six_also_wins_and_winning_line_reports_full_run()
    {
        // Seed X at (0,0)..(0,3) and (0,5); playing (0,4) closes a
        // horizontal run of length 6 through the just-played cell. The
        // module must detect the win and report the entire 6-cell run as
        // the winning line (not just 5).
        var cells = new string?[TicTacToe9x9State.CellCount];
        for (var c = 0; c <= 3; c++) cells[Idx(0, c)] = TicTacToe9x9Sides.X;
        cells[Idx(0, 5)] = TicTacToe9x9Sides.X;
        var preWin = new TicTacToe9x9State(cells);

        var result = _module.ApplyMove(preWin, TicTacToe9x9Sides.X, new TicTacToe9x9Move(Idx(0, 4)));

        result.Accepted.Should().BeTrue();
        result.Ending.Should().BeOfType<Win>().Which.WinningSide.Should().Be(TicTacToe9x9Sides.X);
        var final = (TicTacToe9x9State)result.NewState!;
        final.WinningLine!.Should().HaveCount(6);
        final.WinningLine!.Select(c => c.Col).Should().Equal(0, 1, 2, 3, 4, 5);
        final.WinningLine!.Should().AllSatisfy(c => c.Row.Should().Be(0));
    }

    [Fact]
    public void Run_of_four_does_not_win()
    {
        // Four Xs in a row at (5,0)..(5,3) — should not be a win.
        var cells = new string?[TicTacToe9x9State.CellCount];
        cells[Idx(5, 0)] = TicTacToe9x9Sides.X;
        cells[Idx(5, 1)] = TicTacToe9x9Sides.X;
        cells[Idx(5, 2)] = TicTacToe9x9Sides.X;
        var preMove = new TicTacToe9x9State(cells);

        var result = _module.ApplyMove(preMove, TicTacToe9x9Sides.X, new TicTacToe9x9Move(Idx(5, 3)));

        result.Accepted.Should().BeTrue();
        result.Ending.Should().BeNull();
        ((TicTacToe9x9State)result.NewState!).WinningLine.Should().BeNull();
    }

    [Fact]
    public void Run_of_five_split_by_opponent_does_not_win()
    {
        // X at (0,0),(0,1),(0,2), O at (0,3), X at (0,4) — placing X at
        // (0,5) gives X two short runs (3 and 2 around the O), not 5
        // consecutive.
        var cells = new string?[TicTacToe9x9State.CellCount];
        cells[Idx(0, 0)] = TicTacToe9x9Sides.X;
        cells[Idx(0, 1)] = TicTacToe9x9Sides.X;
        cells[Idx(0, 2)] = TicTacToe9x9Sides.X;
        cells[Idx(0, 3)] = TicTacToe9x9Sides.O;
        cells[Idx(0, 4)] = TicTacToe9x9Sides.X;
        var preMove = new TicTacToe9x9State(cells);

        var result = _module.ApplyMove(preMove, TicTacToe9x9Sides.X, new TicTacToe9x9Move(Idx(0, 5)));

        result.Accepted.Should().BeTrue();
        result.Ending.Should().BeNull();
        ((TicTacToe9x9State)result.NewState!).WinningLine.Should().BeNull();
    }

    [Fact]
    public void Draw_when_board_fills_with_no_winning_line()
    {
        // Construct a hand-picked 81-cell layout that fills the board with
        // no run of five for either side, then play the final cell through
        // the module so win detection runs and the resulting Ending is
        // Draw. Pattern: colour for (row, col) is determined by
        // ((row / 4) + col) mod 2. This makes rows alternate every column
        // (max horizontal run = 1), columns paint in blocks of 4 (max
        // vertical run = 4), and both diagonals alternate within each
        // 4-row block (max diagonal run = 2 at the block boundary). The
        // <see cref="AssertNoRunOfFive"/> helper verifies the property
        // exhaustively before we hand the board to the module.
        var cells = new string?[TicTacToe9x9State.CellCount];
        for (var row = 0; row < TicTacToe9x9State.Size; row++)
        {
            for (var col = 0; col < TicTacToe9x9State.Size; col++)
            {
                cells[Idx(row, col)] = (((row / 4) + col) % 2) == 0
                    ? TicTacToe9x9Sides.X
                    : TicTacToe9x9Sides.O;
            }
        }
        // Sanity: assert the seeded board contains no run of five so the
        // draw test is meaningful. If this fails the pattern is buggy and
        // the rest of the test would be misleading.
        AssertNoRunOfFive(cells);

        // Clear the last cell and replay it through the module so it sees
        // the move as fresh and runs win+draw detection.
        var lastIdx = Idx(8, 8);
        var lastSide = cells[lastIdx]!;
        cells[lastIdx] = null;
        var preDraw = new TicTacToe9x9State(cells);

        var move = _module.ApplyMove(preDraw, lastSide, new TicTacToe9x9Move(lastIdx));

        move.Accepted.Should().BeTrue();
        move.Ending.Should().BeOfType<Draw>();
        ((TicTacToe9x9State)move.NewState!).IsFull().Should().BeTrue();
    }

    [Fact]
    public void Serialize_and_Deserialize_round_trip_preserves_state()
    {
        var state = (IGameState)_module.NewMatch(null);
        state = _module.ApplyMove(state, TicTacToe9x9Sides.X, new TicTacToe9x9Move(Idx(0, 0))).NewState!;
        state = _module.ApplyMove(state, TicTacToe9x9Sides.O, new TicTacToe9x9Move(Idx(1, 1))).NewState!;
        state = _module.ApplyMove(state, TicTacToe9x9Sides.X, new TicTacToe9x9Move(Idx(2, 2))).NewState!;

        var json = _module.Serialize(state);
        var restored = (TicTacToe9x9State)_module.Deserialize(json);
        var original = (TicTacToe9x9State)state;

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

    private static void AssertNoRunOfFive(string?[] cells)
    {
        var dirs = new (int dr, int dc)[] { (0, 1), (1, 0), (1, 1), (-1, 1) };
        for (var r = 0; r < TicTacToe9x9State.Size; r++)
        {
            for (var c = 0; c < TicTacToe9x9State.Size; c++)
            {
                var side = cells[Idx(r, c)];
                if (side is null) continue;
                foreach (var (dr, dc) in dirs)
                {
                    var run = 0;
                    var rr = r;
                    var cc = c;
                    while (rr >= 0 && rr < TicTacToe9x9State.Size
                           && cc >= 0 && cc < TicTacToe9x9State.Size
                           && cells[Idx(rr, cc)] == side)
                    {
                        run++;
                        rr += dr;
                        cc += dc;
                    }
                    run.Should().BeLessThan(5,
                        $"draw seed should not contain a run of 5 from ({r},{c}) in ({dr},{dc})");
                }
            }
        }
    }
}
