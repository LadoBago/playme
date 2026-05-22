using FluentAssertions;
using PlayMe.Domain.Games.Reversi;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Per-game rules unit tests for <see cref="ReversiGameModule"/>. The
/// platform's move pipeline is covered by <c>SubmitMoveHandler</c> tests;
/// these tests pin the Reversi rules directly so opening / bracketing /
/// auto-pass logic can't accidentally regress the platform tests.
///
/// Conventions:
/// - Cells are row-major on an 8×8 board; row 0 is the top, row 7 the bottom.
/// - Central 2×2 squares (opening): rows 3–4 × cols 3–4.
/// - Sides are <see cref="ReversiSides.Dark"/> (first) and
///   <see cref="ReversiSides.Light"/>.
/// </summary>
public sealed class ReversiGameModuleTests
{
    private readonly ReversiGameModule _module = new();

    [Fact]
    public void Module_metadata_is_canonical()
    {
        _module.Id.Value.Should().Be("reversi");
        _module.ValidSides.Should().Equal(ReversiSides.Dark, ReversiSides.Light);
        _module.FirstMoveSide.Should().Be(ReversiSides.Dark);
        _module.DefaultClockBudget.Should().Be(TimeSpan.FromMinutes(10));
        _module.OtherSide(ReversiSides.Dark).Should().Be(ReversiSides.Light);
        _module.OtherSide(ReversiSides.Light).Should().Be(ReversiSides.Dark);
    }

    [Fact]
    public void NewMatch_starts_empty_with_dark_to_move_and_opening_phase()
    {
        var state = (ReversiState)_module.NewMatch(null);

        state.Cells.Should().HaveCount(ReversiState.CellCount);
        state.Cells.Should().AllSatisfy(c => c.Should().BeNull());
        state.MoveCount.Should().Be(0);
        state.LastPlacement.Should().BeNull();
        state.LastWasPass.Should().BeFalse();
        state.FlippedLastTurn.Should().BeEmpty();
        state.ConsecutivePasses.Should().Be(0);
        state.MustPassSide.Should().BeNull();
        state.DarkCount.Should().Be(0);
        state.LightCount.Should().Be(0);
        state.InOpening.Should().BeTrue();
    }

    [Fact]
    public void Opening_placement_outside_central_2x2_is_rejected()
    {
        var state = _module.NewMatch(null);

        var result = _module.ApplyMove(state, ReversiSides.Dark, new ReversiPlacement(0, 0));

        result.Accepted.Should().BeFalse();
        result.RejectKey.Should().Be(ReversiErrors.OpeningMustBeCentral);
    }

    [Theory]
    [InlineData(3, 3)]
    [InlineData(3, 4)]
    [InlineData(4, 3)]
    [InlineData(4, 4)]
    public void Opening_central_placement_is_accepted_and_does_not_flip(int row, int col)
    {
        var state = _module.NewMatch(null);

        var result = _module.ApplyMove(state, ReversiSides.Dark, new ReversiPlacement(row, col));

        result.Accepted.Should().BeTrue();
        var next = (ReversiState)result.NewState!;
        next.CellAt(row, col).Should().Be(ReversiSides.Dark);
        next.DarkCount.Should().Be(1);
        next.LightCount.Should().Be(0);
        next.LastPlacement.Should().Be(new ReversiCoordinate(row, col));
        next.FlippedLastTurn.Should().BeEmpty();
        next.InOpening.Should().BeTrue();
    }

    [Fact]
    public void Opening_completes_in_four_central_moves_then_standard_play_begins()
    {
        // Standard Othello-style diagonal opening: D at (3,4) and (4,3);
        // L at (3,3) and (4,4). The classic rule allows any free central
        // ordering; we pick this one for clarity.
        var state = (IGameState)_module.NewMatch(null);
        state = _module.ApplyMove(state, ReversiSides.Dark, new ReversiPlacement(3, 4)).NewState!;
        state = _module.ApplyMove(state, ReversiSides.Light, new ReversiPlacement(3, 3)).NewState!;
        state = _module.ApplyMove(state, ReversiSides.Dark, new ReversiPlacement(4, 3)).NewState!;
        state = _module.ApplyMove(state, ReversiSides.Light, new ReversiPlacement(4, 4)).NewState!;

        var board = (ReversiState)state;
        board.MoveCount.Should().Be(4);
        board.InOpening.Should().BeFalse();
        board.DarkCount.Should().Be(2);
        board.LightCount.Should().Be(2);
        board.MustPassSide.Should().BeNull();
    }

    [Fact]
    public void Standard_play_rejects_placement_that_flips_nothing()
    {
        var state = OthelloDiagonalOpening();

        var result = _module.ApplyMove(state, ReversiSides.Dark, new ReversiPlacement(0, 0));

        result.Accepted.Should().BeFalse();
        result.RejectKey.Should().Be(ReversiErrors.MustBracket);
    }

    [Fact]
    public void Standard_play_flips_bracketed_opponent_disc_in_one_direction()
    {
        // After OthelloDiagonalOpening: D at (3,4),(4,3); L at (3,3),(4,4).
        // Dark plays (2,4): direction (1,0) → (3,4)=D own (fail).
        //                   direction (1,-1) → (3,3)=L, (4,2)=null (fail).
        //                   direction (1,1) → (3,5)=null (fail).
        // Try (5,3): direction (-1,0) → (4,3)=D own (fail). Direction (-1,1) → (4,4)=L, (3,5)=null (fail). Nope.
        // Try (2,2): direction (1,1) → (3,3)=L, (4,4)=L, (5,5)=null. Long opp run with no D at end (fail).
        //                   direction (1,2) — not a unit vector, skip.
        // Try (3,2): direction (0,1) → (3,3)=L, (3,4)=D. Bracket! Flip (3,3).
        var state = OthelloDiagonalOpening();

        var result = _module.ApplyMove(state, ReversiSides.Dark, new ReversiPlacement(3, 2));

        result.Accepted.Should().BeTrue();
        var next = (ReversiState)result.NewState!;
        next.CellAt(3, 2).Should().Be(ReversiSides.Dark);
        next.CellAt(3, 3).Should().Be(ReversiSides.Dark); // flipped
        next.FlippedLastTurn.Should().BeEquivalentTo(new[] { new ReversiCoordinate(3, 3) });
        next.DarkCount.Should().Be(4);
        next.LightCount.Should().Be(1);
        next.LastPlacement.Should().Be(new ReversiCoordinate(3, 2));
        next.LastWasPass.Should().BeFalse();
        next.ConsecutivePasses.Should().Be(0);
    }

    [Fact]
    public void Standard_play_flips_in_multiple_directions_with_one_placement()
    {
        // Hand-built post-opening state. Dark at (3,3) brackets in three
        // separate directions:
        //   - North (-1, 0): (2,3)=L, (1,3)=D → flips (2,3)
        //   - West  ( 0,-1): (3,2)=L, (3,1)=D → flips (3,2)
        //   - SW    ( 1,-1): (4,2)=L, (5,1)=D → flips (4,2)
        var cells = new string?[ReversiState.CellCount];
        cells[ReversiState.IndexOf(2, 3)] = ReversiSides.Light;
        cells[ReversiState.IndexOf(1, 3)] = ReversiSides.Dark;
        cells[ReversiState.IndexOf(3, 2)] = ReversiSides.Light;
        cells[ReversiState.IndexOf(3, 1)] = ReversiSides.Dark;
        cells[ReversiState.IndexOf(4, 2)] = ReversiSides.Light;
        cells[ReversiState.IndexOf(5, 1)] = ReversiSides.Dark;
        var state = new ReversiState(
            cells,
            moveCount: 6,
            lastPlacement: null,
            lastWasPass: false,
            flippedLastTurn: Array.Empty<ReversiCoordinate>(),
            consecutivePasses: 0,
            mustPassSide: null);

        var result = _module.ApplyMove(state, ReversiSides.Dark, new ReversiPlacement(3, 3));

        result.Accepted.Should().BeTrue();
        var next = (ReversiState)result.NewState!;
        next.CellAt(3, 3).Should().Be(ReversiSides.Dark);
        next.CellAt(2, 3).Should().Be(ReversiSides.Dark);
        next.CellAt(3, 2).Should().Be(ReversiSides.Dark);
        next.CellAt(4, 2).Should().Be(ReversiSides.Dark);
        next.FlippedLastTurn.Should().BeEquivalentTo(new[]
        {
            new ReversiCoordinate(2, 3),
            new ReversiCoordinate(3, 2),
            new ReversiCoordinate(4, 2),
        });
    }

    [Fact]
    public void Placement_out_of_bounds_is_rejected()
    {
        var state = _module.NewMatch(null);

        var result = _module.ApplyMove(state, ReversiSides.Dark, new ReversiPlacement(8, 0));

        result.Accepted.Should().BeFalse();
        result.RejectKey.Should().Be(ReversiErrors.OutOfBounds);
    }

    [Fact]
    public void Placement_on_occupied_cell_is_rejected()
    {
        var state = _module.ApplyMove(_module.NewMatch(null), ReversiSides.Dark, new ReversiPlacement(3, 3)).NewState!;

        var result = _module.ApplyMove(state, ReversiSides.Light, new ReversiPlacement(3, 3));

        result.Accepted.Should().BeFalse();
        result.RejectKey.Should().Be(ReversiErrors.CellOccupied);
    }

    [Fact]
    public void Pass_is_rejected_when_must_pass_side_does_not_match()
    {
        // State says Light must pass; Dark submits a pass. Reject.
        var state = StuckState(ReversiSides.Light);

        var result = _module.ApplyMove(state, ReversiSides.Dark, new ReversiPass());

        result.Accepted.Should().BeFalse();
        result.RejectKey.Should().Be(ReversiErrors.PassNotAllowed);
    }

    [Fact]
    public void Pass_is_rejected_when_side_actually_has_a_legal_move()
    {
        // Defensive: state.MustPassSide says Dark must pass, but the board
        // actually offers Dark a legal placement. Reject the pass.
        // Build a board where Dark genuinely can move (the diagonal-opening
        // post-state offers Dark four legal flips), then tamper the
        // MustPassSide flag.
        var board = (ReversiState)OthelloDiagonalOpening();
        var tampered = new ReversiState(
            board.Cells,
            board.MoveCount,
            board.LastPlacement,
            board.LastWasPass,
            board.FlippedLastTurn,
            board.ConsecutivePasses,
            mustPassSide: ReversiSides.Dark);

        var result = _module.ApplyMove(tampered, ReversiSides.Dark, new ReversiPass());

        result.Accepted.Should().BeFalse();
        result.RejectKey.Should().Be(ReversiErrors.PassNotAllowed);
    }

    [Fact]
    public void Pass_is_accepted_when_side_truly_stuck_and_flag_matches()
    {
        // Board: only Light at (0,0), only Dark at (5,5). Light to move,
        // Light has no legal placement (only one own disc, no bracket
        // pattern reaches it). MustPassSide = Light. ConsecutivePasses = 0.
        // Light passes. Accepted; ConsecutivePasses → 1; LastWasPass = true.
        var state = StuckState(ReversiSides.Light);

        var result = _module.ApplyMove(state, ReversiSides.Light, new ReversiPass());

        result.Accepted.Should().BeTrue();
        var next = (ReversiState)result.NewState!;
        next.LastWasPass.Should().BeTrue();
        next.LastPlacement.Should().BeNull();
        next.FlippedLastTurn.Should().BeEmpty();
        next.ConsecutivePasses.Should().Be(1);
    }

    [Fact]
    public void Two_consecutive_passes_terminate_with_disc_count_outcome()
    {
        // Pre-state: Light has already passed once (ConsecutivePasses=1) and
        // MustPassSide=Dark. Dark passes → ConsecutivePasses=2 → terminal.
        var cells = new string?[ReversiState.CellCount];
        cells[ReversiState.IndexOf(0, 0)] = ReversiSides.Light;
        cells[ReversiState.IndexOf(5, 5)] = ReversiSides.Dark;
        var state = new ReversiState(
            cells,
            moveCount: 30,
            lastPlacement: null,
            lastWasPass: true,
            flippedLastTurn: Array.Empty<ReversiCoordinate>(),
            consecutivePasses: 1,
            mustPassSide: ReversiSides.Dark);

        var result = _module.ApplyMove(state, ReversiSides.Dark, new ReversiPass());

        result.Accepted.Should().BeTrue();
        result.Ending.Should().BeOfType<Draw>(); // 1 dark, 1 light → tie
        var next = (ReversiState)result.NewState!;
        next.ConsecutivePasses.Should().Be(2);
    }

    [Fact]
    public void Both_sides_stuck_after_placement_terminates_immediately()
    {
        // Construct a pre-state where Dark places at (1,0) flipping the
        // single Light at (0,0)... no, (0,0) can't be a flip target since
        // there's nothing further to anchor on. Use a flip-and-strand
        // pattern: pre-state has L at (4,4) (single light to be flipped),
        // D at (3,3) (anchor on one diagonal). Dark plays (5,5):
        //   diagonal (-1,-1): (4,4)=L, (3,3)=D → bracket, flip (4,4) → D.
        // Post-state: all discs are Dark (no Light remains anywhere); both
        // sides have no legal moves. Should terminate immediately as a Win
        // for Dark via the disc count.
        var cells = new string?[ReversiState.CellCount];
        cells[ReversiState.IndexOf(3, 3)] = ReversiSides.Dark;
        cells[ReversiState.IndexOf(4, 4)] = ReversiSides.Light;
        var state = new ReversiState(
            cells,
            moveCount: 4,
            lastPlacement: null,
            lastWasPass: false,
            flippedLastTurn: Array.Empty<ReversiCoordinate>(),
            consecutivePasses: 0,
            mustPassSide: null);

        var result = _module.ApplyMove(state, ReversiSides.Dark, new ReversiPlacement(5, 5));

        result.Accepted.Should().BeTrue();
        result.Ending.Should().BeOfType<Win>().Which.WinningSide.Should().Be(ReversiSides.Dark);
        var next = (ReversiState)result.NewState!;
        next.DarkCount.Should().Be(3);
        next.LightCount.Should().Be(0);
        next.MustPassSide.Should().BeNull();
    }

    [Fact]
    public void Terminal_with_light_majority_yields_light_win()
    {
        // Mirror image: 1 dark to be flipped to light, 1 light anchor.
        var cells = new string?[ReversiState.CellCount];
        cells[ReversiState.IndexOf(3, 3)] = ReversiSides.Light;
        cells[ReversiState.IndexOf(4, 4)] = ReversiSides.Dark;
        var state = new ReversiState(
            cells,
            moveCount: 4,
            lastPlacement: null,
            lastWasPass: false,
            flippedLastTurn: Array.Empty<ReversiCoordinate>(),
            consecutivePasses: 0,
            mustPassSide: null);

        var result = _module.ApplyMove(state, ReversiSides.Light, new ReversiPlacement(5, 5));

        result.Accepted.Should().BeTrue();
        result.Ending.Should().BeOfType<Win>().Which.WinningSide.Should().Be(ReversiSides.Light);
        var next = (ReversiState)result.NewState!;
        next.LightCount.Should().Be(3);
        next.DarkCount.Should().Be(0);
    }

    [Fact]
    public void Terminal_with_equal_counts_yields_draw()
    {
        // Pre-state where both sides will be stuck after the placement and
        // disc counts come out equal. After Dark plays (2,3) flipping the
        // single Light at (3,3) via the (1,0) anchor at (4,3)... actually
        // we want disc counts EQUAL, so flip a light AND keep another light
        // alive in an unreachable spot.
        // Setup:
        //   D at (4,3) (anchor); L at (3,3) (to flip); L at (0,7) (lone).
        // Dark plays (2,3): walk (1,0) → (3,3)=L, (4,3)=D → bracket, flip (3,3) → D.
        // Post-state: D at (2,3),(3,3),(4,3); L at (0,7). Counts: 3 D, 1 L.
        // Not equal. Need 2 D, 2 L after flip.
        // Setup with 2 lights remaining:
        //   D at (4,3); L at (3,3) (to flip); L at (0,7); L at (1,7); plus D somewhere.
        //   But then Light has discs adjacent and may have moves. Need them stranded.
        // Simpler: two isolated lights and two darks.
        //   Pre: D at (4,3); L at (3,3) (will flip); L at (0,7); D at (7,0).
        //   Dark plays (2,3): flip (3,3) → D. Now D at (2,3),(3,3),(4,3),(7,0); L at (0,7).
        //   Counts: 4 D, 1 L. Not equal.
        // We want EQUAL counts after the flip:
        //   D at (4,3); L at (3,3); L at (0,7); L at (7,0); plus D at (0,0).
        //   Dark plays (2,3): flip (3,3) → D. D at (0,0),(2,3),(3,3),(4,3); L at (0,7),(7,0).
        //   Counts: 4 D, 2 L. Not equal.
        // Need 1 flip + (n D, n L) before such that after flip you have (n+1+1 D, n-1 L) wait let me re-think.
        // Before placement: x D, y L. Placement flips k Ls. After: D = x + 1 + k, L = y - k.
        // For equal: x + 1 + k = y - k → y = x + 1 + 2k.
        // We need k ≥ 1 (the placement must flip something post-opening). Try k=1: y = x + 3.
        // So pre-state has x Ds, x+3 Ls (one of which is the one to be flipped).
        // Smallest x: 1. Pre: 1 D, 4 L. Post placement: 1+1+1=3 D, 4-1=3 L. Equal!
        // Plus we need both sides stuck post-placement (so terminal triggers).
        // Construction:
        //   D anchor at (4,3) (1 D).
        //   L to be flipped: (3,3).
        //   Other 3 Ls placed so both sides are stuck after: (0,7), (7,0), (7,7).
        //   Dark plays (2,3) → walk (1,0): (3,3)=L, (4,3)=D → bracket, flip (3,3) → D.
        //   Post: D at (2,3),(3,3),(4,3); L at (0,7),(7,0),(7,7). Counts: 3D, 3L. Equal.
        //   Both stuck? Need to verify.
        //   Dark's options: place anywhere empty + bracket Ls.
        //     - The Ls are isolated at corners (0,7), (7,0), (7,7). No bracket chain reaches them.
        //   Light's options: place anywhere empty + bracket Ds.
        //     - Ds at (2,3),(3,3),(4,3) — vertical row, col 3. Light could play (1,3) bracket (1,0)? walk: (2,3)=D, (3,3)=D, (4,3)=D, (5,3)=null. No L at end. Reject. (5,3) bracket (-1,0)? (4,3)=D, (3,3)=D, (2,3)=D, (1,3)=null. No L. Reject.
        //     - L could play (0,7) is occupied. (1,7)? walk (-1,0): (0,7)=L own (no opp run). Walk (1,0): (2,7)=null. Other dirs: (0,-1): (1,6)=null. Etc. No legal moves for L at (1,7).
        //     - L at (1,6)? Walk (1,1): (2,7)=null. (-1,1): (0,7)=L own. (-1,-1)? (0,5)=null. No bracket.
        //   By similar exhaustion both sides stuck. (Verified by hand.)
        var cells = new string?[ReversiState.CellCount];
        cells[ReversiState.IndexOf(4, 3)] = ReversiSides.Dark;
        cells[ReversiState.IndexOf(3, 3)] = ReversiSides.Light;
        cells[ReversiState.IndexOf(0, 7)] = ReversiSides.Light;
        cells[ReversiState.IndexOf(7, 0)] = ReversiSides.Light;
        cells[ReversiState.IndexOf(7, 7)] = ReversiSides.Light;
        var state = new ReversiState(
            cells,
            moveCount: 4,
            lastPlacement: null,
            lastWasPass: false,
            flippedLastTurn: Array.Empty<ReversiCoordinate>(),
            consecutivePasses: 0,
            mustPassSide: null);

        var result = _module.ApplyMove(state, ReversiSides.Dark, new ReversiPlacement(2, 3));

        result.Accepted.Should().BeTrue();
        var next = (ReversiState)result.NewState!;
        next.DarkCount.Should().Be(3);
        next.LightCount.Should().Be(3);
        result.Ending.Should().BeOfType<Draw>();
    }

    [Fact]
    public void Board_full_after_placement_terminates_with_disc_count()
    {
        // Construct a 63-filled board where Dark's placement at (0,0) fills
        // the last cell. To keep the test concise we don't require the
        // placement to flip — we use a full-board pre-state with a single
        // empty cell, and rely on the rules engine accepting it via... hmm,
        // actually post-opening requires a flip. Easier: fill 62 cells,
        // leave (0,0) and one bracketable target for Dark. Use the
        // OpeningSetup → flip pattern with auxiliary fillers.
        //
        // We just need to exercise the CellsFull → terminal branch. Simpler:
        // construct a 63-cell-filled state where the placement DOES bracket
        // a light disc, and verify CellsFull triggers termination with the
        // correct outcome.
        //
        // Setup: cell (0,0) is the empty target. Place Dark at (0,1) and
        // Dark at (0,3); Light at (0,2). 60 other cells filled with Dark
        // (count doesn't matter for the rule, just for the outcome).
        var cells = new string?[ReversiState.CellCount];
        for (var i = 0; i < ReversiState.CellCount; i++)
        {
            cells[i] = ReversiSides.Dark;
        }
        cells[ReversiState.IndexOf(0, 0)] = null;        // empty target
        cells[ReversiState.IndexOf(0, 2)] = ReversiSides.Light; // the flip
        // (0,1) and (0,3) stay Dark from the fill, providing the bracket
        // anchor for Dark's placement at (0,0) along direction (0,1):
        // (0,1)=D own — immediate stop, no bracket. That breaks the test.
        // Instead: place Light at (0,1) so Dark at (0,0) walks (0,1) → L,
        // and we need a D further out. Put L at (0,1) and the chain D at
        // (0,2). Direction (0,1) from (0,0): (0,1)=L, (0,2)=D → bracket
        // flips (0,1).
        cells[ReversiState.IndexOf(0, 1)] = ReversiSides.Light;
        cells[ReversiState.IndexOf(0, 2)] = ReversiSides.Dark;
        var state = new ReversiState(
            cells,
            moveCount: 63,
            lastPlacement: null,
            lastWasPass: false,
            flippedLastTurn: Array.Empty<ReversiCoordinate>(),
            consecutivePasses: 0,
            mustPassSide: null);

        var result = _module.ApplyMove(state, ReversiSides.Dark, new ReversiPlacement(0, 0));

        result.Accepted.Should().BeTrue();
        var next = (ReversiState)result.NewState!;
        next.IsFull().Should().BeTrue();
        next.CellAt(0, 1).Should().Be(ReversiSides.Dark); // flipped
        result.Ending.Should().BeOfType<Win>().Which.WinningSide.Should().Be(ReversiSides.Dark);
    }

    [Fact]
    public void Serialize_and_Deserialize_round_trip_preserves_state()
    {
        var state = (IGameState)_module.NewMatch(null);
        state = _module.ApplyMove(state, ReversiSides.Dark, new ReversiPlacement(3, 4)).NewState!;
        state = _module.ApplyMove(state, ReversiSides.Light, new ReversiPlacement(3, 3)).NewState!;
        state = _module.ApplyMove(state, ReversiSides.Dark, new ReversiPlacement(4, 3)).NewState!;
        state = _module.ApplyMove(state, ReversiSides.Light, new ReversiPlacement(4, 4)).NewState!;
        state = _module.ApplyMove(state, ReversiSides.Dark, new ReversiPlacement(3, 2)).NewState!;

        var json = _module.Serialize(state);
        var restored = (ReversiState)_module.Deserialize(json);
        var original = (ReversiState)state;

        restored.Cells.Should().Equal(original.Cells);
        restored.MoveCount.Should().Be(original.MoveCount);
        restored.LastPlacement.Should().Be(original.LastPlacement);
        restored.LastWasPass.Should().Be(original.LastWasPass);
        restored.FlippedLastTurn.Should().BeEquivalentTo(original.FlippedLastTurn);
        restored.ConsecutivePasses.Should().Be(original.ConsecutivePasses);
        restored.MustPassSide.Should().Be(original.MustPassSide);
        restored.DarkCount.Should().Be(original.DarkCount);
        restored.LightCount.Should().Be(original.LightCount);
    }

    [Fact]
    public void Deserialize_rejects_wrong_board_size()
    {
        var bogus = """{"size":3,"moveCount":0,"cells":[null,null,null,null,null,null,null,null,null]}""";

        var act = () => _module.Deserialize(bogus);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Plays the four central placements that complete the opening with
    /// dark on the off-diagonal (3,4 / 4,3) and light on the main diagonal
    /// (3,3 / 4,4). Returns the post-opening state with dark to move.
    /// </summary>
    private IGameState OthelloDiagonalOpening()
    {
        var state = (IGameState)_module.NewMatch(null);
        state = _module.ApplyMove(state, ReversiSides.Dark, new ReversiPlacement(3, 4)).NewState!;
        state = _module.ApplyMove(state, ReversiSides.Light, new ReversiPlacement(3, 3)).NewState!;
        state = _module.ApplyMove(state, ReversiSides.Dark, new ReversiPlacement(4, 3)).NewState!;
        state = _module.ApplyMove(state, ReversiSides.Light, new ReversiPlacement(4, 4)).NewState!;
        return state;
    }

    /// <summary>
    /// Hand-built post-opening state with a single Dark at (5,5) and a
    /// single Light at (0,0). Both sides are stuck (only one own disc each,
    /// no bracket pattern can reach the other side). Used by pass-handling
    /// tests; the parameter selects which side the <c>MustPassSide</c> flag
    /// is set against.
    /// </summary>
    private static ReversiState StuckState(string mustPassSide)
    {
        var cells = new string?[ReversiState.CellCount];
        cells[ReversiState.IndexOf(0, 0)] = ReversiSides.Light;
        cells[ReversiState.IndexOf(5, 5)] = ReversiSides.Dark;
        return new ReversiState(
            cells,
            moveCount: 20,
            lastPlacement: null,
            lastWasPass: false,
            flippedLastTurn: Array.Empty<ReversiCoordinate>(),
            consecutivePasses: 0,
            mustPassSide: mustPassSide);
    }
}
