using FluentAssertions;
using PlayMe.Domain.Games.Connect4;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Per-game rules unit tests for <see cref="Connect4GameModule"/>. The
/// platform's move pipeline is covered by <c>SubmitMoveHandlerClockTests</c>;
/// these tests pin the module's rules directly so adding/changing rules
/// can't accidentally regress the platform tests.
///
/// Conventions:
/// - Cells are row-major; row 0 is the top of the board, row 5 the bottom.
/// - Gravity lands a disc at the largest empty row index of the chosen column.
/// - Sides are <see cref="Connect4Sides.Red"/> (first) and
///   <see cref="Connect4Sides.Yellow"/>.
/// </summary>
public sealed class Connect4GameModuleTests
{
    private readonly Connect4GameModule _module = new();

    [Fact]
    public void NewMatch_starts_with_empty_board_and_red_first()
    {
        var state = (Connect4State)_module.NewMatch(null);

        state.Cells.Should().HaveCount(Connect4State.CellCount);
        state.Cells.Should().AllSatisfy(c => c.Should().BeNull());
        state.LastMove.Should().BeNull();
        state.WinningLine.Should().BeNull();
        _module.FirstMoveSide.Should().Be(Connect4Sides.Red);
    }

    [Fact]
    public void Module_metadata_is_canonical()
    {
        _module.Id.Value.Should().Be("connect4");
        _module.ValidSides.Should().Equal(Connect4Sides.Red, Connect4Sides.Yellow);
        _module.DefaultClockBudget.Should().Be(TimeSpan.FromMinutes(3));
        _module.OtherSide(Connect4Sides.Red).Should().Be(Connect4Sides.Yellow);
        _module.OtherSide(Connect4Sides.Yellow).Should().Be(Connect4Sides.Red);
    }

    [Fact]
    public void ApplyMove_drops_disc_to_bottom_of_empty_column()
    {
        var state = _module.NewMatch(null);

        var result = _module.ApplyMove(state, Connect4Sides.Red, new Connect4Move(3));

        result.Accepted.Should().BeTrue();
        result.Ending.Should().BeNull();
        var next = (Connect4State)result.NewState!;
        next.CellAt(5, 3).Should().Be(Connect4Sides.Red);
        next.LastMove.Should().Be(new Connect4Coordinate(5, 3));
        // All other cells stay empty.
        for (var row = 0; row < Connect4State.Rows; row++)
        {
            for (var col = 0; col < Connect4State.Cols; col++)
            {
                if (row == 5 && col == 3) continue;
                next.CellAt(row, col).Should().BeNull();
            }
        }
    }

    [Fact]
    public void ApplyMove_stacks_subsequent_discs_in_same_column()
    {
        IGameState state = _module.NewMatch(null);
        state = _module.ApplyMove(state, Connect4Sides.Red, new Connect4Move(0)).NewState!;
        state = _module.ApplyMove(state, Connect4Sides.Yellow, new Connect4Move(0)).NewState!;
        state = _module.ApplyMove(state, Connect4Sides.Red, new Connect4Move(0)).NewState!;

        var board = (Connect4State)state;
        board.CellAt(5, 0).Should().Be(Connect4Sides.Red);
        board.CellAt(4, 0).Should().Be(Connect4Sides.Yellow);
        board.CellAt(3, 0).Should().Be(Connect4Sides.Red);
        board.CellAt(2, 0).Should().BeNull();
        board.LastMove.Should().Be(new Connect4Coordinate(3, 0));
    }

    [Fact]
    public void ApplyMove_rejects_negative_column()
    {
        var state = _module.NewMatch(null);

        var result = _module.ApplyMove(state, Connect4Sides.Red, new Connect4Move(-1));

        result.Accepted.Should().BeFalse();
        result.RejectKey.Should().Be(Connect4Errors.IllegalColumn);
    }

    [Fact]
    public void ApplyMove_rejects_column_past_right_edge()
    {
        var state = _module.NewMatch(null);

        var result = _module.ApplyMove(state, Connect4Sides.Red, new Connect4Move(Connect4State.Cols));

        result.Accepted.Should().BeFalse();
        result.RejectKey.Should().Be(Connect4Errors.IllegalColumn);
    }

    [Fact]
    public void ApplyMove_rejects_full_column()
    {
        var state = (IGameState)_module.NewMatch(null);
        // Fill column 4 — 6 discs alternating Red/Yellow.
        var side = Connect4Sides.Red;
        for (var i = 0; i < Connect4State.Rows; i++)
        {
            state = _module.ApplyMove(state, side, new Connect4Move(4)).NewState!;
            side = _module.OtherSide(side);
        }

        var result = _module.ApplyMove(state, side, new Connect4Move(4));

        result.Accepted.Should().BeFalse();
        result.RejectKey.Should().Be(Connect4Errors.ColumnFull);
    }

    [Fact]
    public void ApplyMove_horizontal_four_wins()
    {
        // Red builds 4-in-a-row across cols 0..3 of the bottom row, Yellow
        // wastes moves stacking col 6.
        var state = (IGameState)_module.NewMatch(null);
        state = _module.ApplyMove(state, Connect4Sides.Red, new Connect4Move(0)).NewState!;
        state = _module.ApplyMove(state, Connect4Sides.Yellow, new Connect4Move(6)).NewState!;
        state = _module.ApplyMove(state, Connect4Sides.Red, new Connect4Move(1)).NewState!;
        state = _module.ApplyMove(state, Connect4Sides.Yellow, new Connect4Move(6)).NewState!;
        state = _module.ApplyMove(state, Connect4Sides.Red, new Connect4Move(2)).NewState!;
        state = _module.ApplyMove(state, Connect4Sides.Yellow, new Connect4Move(6)).NewState!;

        var winning = _module.ApplyMove(state, Connect4Sides.Red, new Connect4Move(3));

        winning.Accepted.Should().BeTrue();
        winning.Ending.Should().BeOfType<Win>().Which.WinningSide.Should().Be(Connect4Sides.Red);
        var final = (Connect4State)winning.NewState!;
        final.WinningLine.Should().BeEquivalentTo(new[]
        {
            new Connect4Coordinate(5, 0),
            new Connect4Coordinate(5, 1),
            new Connect4Coordinate(5, 2),
            new Connect4Coordinate(5, 3),
        }, opt => opt.WithStrictOrdering());
    }

    [Fact]
    public void ApplyMove_vertical_four_wins()
    {
        // Yellow stacks col 2 four times; Red wastes moves on col 5.
        var state = (IGameState)_module.NewMatch(null);
        state = _module.ApplyMove(state, Connect4Sides.Red, new Connect4Move(5)).NewState!;
        state = _module.ApplyMove(state, Connect4Sides.Yellow, new Connect4Move(2)).NewState!;
        state = _module.ApplyMove(state, Connect4Sides.Red, new Connect4Move(5)).NewState!;
        state = _module.ApplyMove(state, Connect4Sides.Yellow, new Connect4Move(2)).NewState!;
        state = _module.ApplyMove(state, Connect4Sides.Red, new Connect4Move(5)).NewState!;
        state = _module.ApplyMove(state, Connect4Sides.Yellow, new Connect4Move(2)).NewState!;
        state = _module.ApplyMove(state, Connect4Sides.Red, new Connect4Move(4)).NewState!;

        var winning = _module.ApplyMove(state, Connect4Sides.Yellow, new Connect4Move(2));

        winning.Accepted.Should().BeTrue();
        winning.Ending.Should().BeOfType<Win>().Which.WinningSide.Should().Be(Connect4Sides.Yellow);
        var final = (Connect4State)winning.NewState!;
        final.WinningLine.Should().BeEquivalentTo(new[]
        {
            new Connect4Coordinate(2, 2),
            new Connect4Coordinate(3, 2),
            new Connect4Coordinate(4, 2),
            new Connect4Coordinate(5, 2),
        }, opt => opt.WithStrictOrdering());
    }

    [Fact]
    public void ApplyMove_diagonal_down_right_four_wins()
    {
        // Construct the pre-winning board directly — these tests check the
        // win-detection rule, not legal-move sequencing. The board has Red
        // discs at the three earlier diagonal cells (2,0), (3,1), (4,2)
        // with Yellow fillers below each so gravity is consistent. Red's
        // next drop into col 3 lands at (5,3) and closes the ↘ diagonal.
        var cells = new string?[Connect4State.CellCount];
        // Col 0 — Yellow at rows 5,4,3 then Red at row 2.
        cells[Connect4State.IndexOf(5, 0)] = Connect4Sides.Yellow;
        cells[Connect4State.IndexOf(4, 0)] = Connect4Sides.Yellow;
        cells[Connect4State.IndexOf(3, 0)] = Connect4Sides.Yellow;
        cells[Connect4State.IndexOf(2, 0)] = Connect4Sides.Red;
        // Col 1 — Yellow at rows 5,4 then Red at row 3.
        cells[Connect4State.IndexOf(5, 1)] = Connect4Sides.Yellow;
        cells[Connect4State.IndexOf(4, 1)] = Connect4Sides.Yellow;
        cells[Connect4State.IndexOf(3, 1)] = Connect4Sides.Red;
        // Col 2 — Yellow at row 5 then Red at row 4.
        cells[Connect4State.IndexOf(5, 2)] = Connect4Sides.Yellow;
        cells[Connect4State.IndexOf(4, 2)] = Connect4Sides.Red;
        // Col 3 — empty; the winning Red lands at (5,3).
        var preWin = new Connect4State(cells);

        var winning = _module.ApplyMove(preWin, Connect4Sides.Red, new Connect4Move(3));

        winning.Accepted.Should().BeTrue();
        winning.Ending.Should().BeOfType<Win>().Which.WinningSide.Should().Be(Connect4Sides.Red);
        var final = (Connect4State)winning.NewState!;
        final.WinningLine.Should().BeEquivalentTo(new[]
        {
            new Connect4Coordinate(2, 0),
            new Connect4Coordinate(3, 1),
            new Connect4Coordinate(4, 2),
            new Connect4Coordinate(5, 3),
        }, opt => opt.WithStrictOrdering());
    }

    [Fact]
    public void ApplyMove_diagonal_up_right_four_wins()
    {
        // Construct the pre-winning board directly. Red has three discs on
        // the ↗ diagonal at (5,0), (4,1), (3,2) with Yellow fillers below
        // each non-bottom cell. Red's next drop into col 3 lands at (2,3)
        // (col 3 has Yellow filling rows 5,4,3) and closes the diagonal.
        var cells = new string?[Connect4State.CellCount];
        cells[Connect4State.IndexOf(5, 0)] = Connect4Sides.Red;
        // Col 1 — Yellow at row 5, Red at (4,1).
        cells[Connect4State.IndexOf(5, 1)] = Connect4Sides.Yellow;
        cells[Connect4State.IndexOf(4, 1)] = Connect4Sides.Red;
        // Col 2 — Yellow at rows 5,4 then Red at (3,2).
        cells[Connect4State.IndexOf(5, 2)] = Connect4Sides.Yellow;
        cells[Connect4State.IndexOf(4, 2)] = Connect4Sides.Yellow;
        cells[Connect4State.IndexOf(3, 2)] = Connect4Sides.Red;
        // Col 3 — Yellow at rows 5,4,3 so the next drop lands at row 2.
        cells[Connect4State.IndexOf(5, 3)] = Connect4Sides.Yellow;
        cells[Connect4State.IndexOf(4, 3)] = Connect4Sides.Yellow;
        cells[Connect4State.IndexOf(3, 3)] = Connect4Sides.Yellow;
        var preWin = new Connect4State(cells);

        var winning = _module.ApplyMove(preWin, Connect4Sides.Red, new Connect4Move(3));

        winning.Accepted.Should().BeTrue();
        winning.Ending.Should().BeOfType<Win>().Which.WinningSide.Should().Be(Connect4Sides.Red);
        var final = (Connect4State)winning.NewState!;
        final.WinningLine.Should().Contain(new Connect4Coordinate(5, 0));
        final.WinningLine.Should().Contain(new Connect4Coordinate(4, 1));
        final.WinningLine.Should().Contain(new Connect4Coordinate(3, 2));
        final.WinningLine.Should().Contain(new Connect4Coordinate(2, 3));
    }

    [Fact]
    public void Win_detection_only_fires_when_run_reaches_four()
    {
        // Three Reds in a row should not be a win.
        var state = (IGameState)_module.NewMatch(null);
        state = _module.ApplyMove(state, Connect4Sides.Red, new Connect4Move(0)).NewState!;
        state = _module.ApplyMove(state, Connect4Sides.Yellow, new Connect4Move(6)).NewState!;
        state = _module.ApplyMove(state, Connect4Sides.Red, new Connect4Move(1)).NewState!;
        state = _module.ApplyMove(state, Connect4Sides.Yellow, new Connect4Move(6)).NewState!;

        var third = _module.ApplyMove(state, Connect4Sides.Red, new Connect4Move(2));

        third.Accepted.Should().BeTrue();
        third.Ending.Should().BeNull();
        ((Connect4State)third.NewState!).WinningLine.Should().BeNull();
    }

    [Fact]
    public void Draw_when_board_fills_with_no_winning_line()
    {
        // Construct a full-board draw by hand. The simple pattern:
        //   columns 0,1 share a vertical sequence Y,Y,Y,R,Y,Y... no
        // It's easier to construct the final 42-cell board directly and
        // assert that the *last* move triggers Draw — bypassing 41 turns
        // worth of arithmetic. We use Connect4State's ctor (per-module
        // helper, not exposed to the platform) to place the first 41
        // discs, then play move 42 via the module so win detection runs.
        //
        // Layout chosen: column j holds, top→bottom, the sequence
        //   r=0: Yellow if j<3 else Red
        //   r=1: Red    if j<3 else Yellow
        //   r=2: Yellow if j<3 else Red
        //   r=3: Red    if j<3 else Yellow
        //   r=4: Yellow if j<3 else Red
        //   r=5: Red    if j<3 else Yellow
        // This staircases the colour boundary so no 4-in-a-row forms.
        // Leave row 0 of column 3 empty as the last move; Yellow will play
        // it (column 3, j<3 is false, so the natural colour would be Red,
        // but we'll arrange the seed so Yellow is the natural mover and
        // no win results).
        //
        // For simplicity: the rules already track win detection; we test
        // Draw by filling 41 cells with no win and verifying the 42nd move
        // is accepted with `Ending = Draw`.
        var cells = new string?[Connect4State.CellCount];
        // Pattern that produces no horizontal/vertical/diagonal 4 of the
        // same colour: a 2x2 checker tile, then shift columns. We tile
        // (Yellow, Red, Yellow, Red, ...) on even rows and the inverse on
        // odd rows, then break the pattern every 2 columns by swapping the
        // top cell colour. With 7 cols × 6 rows there is always a way to
        // construct such a draw; this hand-picked layout suffices.
        string[][] layout =
        {
            // row 0 .. row 5 for each column
            new[] { "y","r","y","y","r","y" }, // col 0
            new[] { "r","y","r","r","y","r" }, // col 1
            new[] { "y","r","y","y","r","y" }, // col 2
            new[] { "r","y","r","r","y","r" }, // col 3
            new[] { "y","r","y","y","r","y" }, // col 4
            new[] { "r","y","r","r","y","r" }, // col 5
            new[] { "y","r","y","y","r","y" }, // col 6
        };
        // Sanity: no four-in-a-row exists in `layout`. Verified by hand
        // against horizontal/vertical/diagonal lines.
        for (var col = 0; col < Connect4State.Cols; col++)
        {
            for (var row = 0; row < Connect4State.Rows; row++)
            {
                cells[Connect4State.IndexOf(row, col)] = layout[col][row] switch
                {
                    "r" => Connect4Sides.Red,
                    "y" => Connect4Sides.Yellow,
                    _ => null,
                };
            }
        }
        // Clear (0,3) — that's the cell the draw-completing move will fill.
        cells[Connect4State.IndexOf(0, 3)] = null;
        var preDraw = new Connect4State(cells);

        var move = _module.ApplyMove(preDraw, Connect4Sides.Red, new Connect4Move(3));

        move.Accepted.Should().BeTrue();
        move.Ending.Should().BeOfType<Draw>();
        ((Connect4State)move.NewState!).IsFull().Should().BeTrue();
    }

    [Fact]
    public void Serialize_and_Deserialize_round_trip_preserves_state()
    {
        var state = (IGameState)_module.NewMatch(null);
        state = _module.ApplyMove(state, Connect4Sides.Red, new Connect4Move(3)).NewState!;
        state = _module.ApplyMove(state, Connect4Sides.Yellow, new Connect4Move(3)).NewState!;
        state = _module.ApplyMove(state, Connect4Sides.Red, new Connect4Move(2)).NewState!;

        var json = _module.Serialize(state);
        var restored = (Connect4State)_module.Deserialize(json);
        var original = (Connect4State)state;

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
