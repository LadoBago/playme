using FluentAssertions;
using PlayMe.Domain.Games.Reversi;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Per-game rules unit tests for <see cref="ReversiGameModule"/>. The
/// platform's move pipeline is covered by <c>SubmitMoveHandler</c> tests;
/// these tests pin the Reversi rules directly so opening / bracketing /
/// forced-skip logic can't accidentally regress the platform tests.
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
        state.FlippedLastTurn.Should().BeEmpty();
        state.SkippedSide.Should().BeNull();
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
        board.SkippedSide.Should().BeNull();
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
        next.SkippedSide.Should().BeNull();
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
            flippedLastTurn: Array.Empty<ReversiCoordinate>());

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
    public void Strand_opponent_keeps_turn_and_sets_skippedSide()
    {
        // Pre-state (Dark to move): D at (0,0); L at (1,1) (to flip); L at
        // (3,3) (survives, isolated). Dark plays (2,2):
        //   diagonal (-1,-1): (1,1)=L, (0,0)=D → bracket, flip (1,1) → D.
        //   diagonal (1,1): (3,3)=L, (4,4)=empty → no bracket.
        // Post-state: D at (0,0),(1,1),(2,2); L at (3,3).
        //   Light is stranded: the only Dark run ending at L(3,3) is the
        //   main diagonal, and the cell beyond (0,0) is off-board; no other
        //   Dark disc is line-adjacent to an empty cell with an L anchor.
        //   Dark can still move: (4,4) brackets (3,3) against (2,2).
        // → the module skips Light's turn: KeepTurn, SkippedSide = Light.
        var state = StrandSetup();

        var result = _module.ApplyMove(state, ReversiSides.Dark, new ReversiPlacement(2, 2));

        result.Accepted.Should().BeTrue();
        result.KeepTurn.Should().BeTrue();
        result.Ending.Should().BeNull();
        var next = (ReversiState)result.NewState!;
        next.SkippedSide.Should().Be(ReversiSides.Light);
        next.CellAt(1, 1).Should().Be(ReversiSides.Dark); // flipped
        next.LastPlacement.Should().Be(new ReversiCoordinate(2, 2));
        next.FlippedLastTurn.Should().BeEquivalentTo(new[] { new ReversiCoordinate(1, 1) });
    }

    [Fact]
    public void Chained_strands_keep_turn_until_terminal()
    {
        // Same setup plus a third isolated Light at (5,5). Dark strands
        // Light twice in a row (each placement flips the next diagonal
        // Light, leaving the one after it isolated but Dark-bracketable),
        // then the third placement flips the last Light — nobody can move,
        // so the match ends there. This also pins the both-stuck terminal
        // that replaced the old two-consecutive-passes trigger.
        var state = (IGameState)StrandSetup(extraLightAt: new ReversiCoordinate(5, 5));

        var first = _module.ApplyMove(state, ReversiSides.Dark, new ReversiPlacement(2, 2));
        first.KeepTurn.Should().BeTrue();
        ((ReversiState)first.NewState!).SkippedSide.Should().Be(ReversiSides.Light);

        var second = _module.ApplyMove(first.NewState!, ReversiSides.Dark, new ReversiPlacement(4, 4));
        second.KeepTurn.Should().BeTrue();
        second.Ending.Should().BeNull();
        ((ReversiState)second.NewState!).SkippedSide.Should().Be(ReversiSides.Light);

        var third = _module.ApplyMove(second.NewState!, ReversiSides.Dark, new ReversiPlacement(6, 6));
        third.Accepted.Should().BeTrue();
        third.Ending.Should().BeOfType<Win>().Which.WinningSide.Should().Be(ReversiSides.Dark);
        var terminal = (ReversiState)third.NewState!;
        terminal.DarkCount.Should().Be(7);
        terminal.LightCount.Should().Be(0);
        terminal.SkippedSide.Should().BeNull();
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
            flippedLastTurn: Array.Empty<ReversiCoordinate>());

        var result = _module.ApplyMove(state, ReversiSides.Dark, new ReversiPlacement(5, 5));

        result.Accepted.Should().BeTrue();
        result.Ending.Should().BeOfType<Win>().Which.WinningSide.Should().Be(ReversiSides.Dark);
        var next = (ReversiState)result.NewState!;
        next.DarkCount.Should().Be(3);
        next.LightCount.Should().Be(0);
        next.SkippedSide.Should().BeNull();
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
            flippedLastTurn: Array.Empty<ReversiCoordinate>());

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
            flippedLastTurn: Array.Empty<ReversiCoordinate>());

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
            flippedLastTurn: Array.Empty<ReversiCoordinate>());

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
        // Use the stranding placement so the round trip covers a non-null
        // SkippedSide, not just the happy alternating path.
        var state = (IGameState)StrandSetup();
        state = _module.ApplyMove(state, ReversiSides.Dark, new ReversiPlacement(2, 2)).NewState!;

        var json = _module.Serialize(state);
        var restored = (ReversiState)_module.Deserialize(json);
        var original = (ReversiState)state;

        original.SkippedSide.Should().Be(ReversiSides.Light); // setup sanity
        restored.Cells.Should().Equal(original.Cells);
        restored.MoveCount.Should().Be(original.MoveCount);
        restored.LastPlacement.Should().Be(original.LastPlacement);
        restored.FlippedLastTurn.Should().BeEquivalentTo(original.FlippedLastTurn);
        restored.SkippedSide.Should().Be(original.SkippedSide);
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
    /// Hand-built post-opening state (Dark to move): D at (0,0), L at (1,1),
    /// L at (3,3), optionally one more Light. Dark's placement at (2,2)
    /// flips (1,1) and strands Light — every Dark line ending at a surviving
    /// Light runs off-board behind (0,0) — while Dark keeps a legal move one
    /// step further down the diagonal. Used by the forced-skip tests.
    /// </summary>
    private static ReversiState StrandSetup(ReversiCoordinate? extraLightAt = null)
    {
        var cells = new string?[ReversiState.CellCount];
        cells[ReversiState.IndexOf(0, 0)] = ReversiSides.Dark;
        cells[ReversiState.IndexOf(1, 1)] = ReversiSides.Light;
        cells[ReversiState.IndexOf(3, 3)] = ReversiSides.Light;
        if (extraLightAt is { } extra)
        {
            cells[ReversiState.IndexOf(extra.Row, extra.Col)] = ReversiSides.Light;
        }
        return new ReversiState(
            cells,
            moveCount: 10,
            lastPlacement: null,
            flippedLastTurn: Array.Empty<ReversiCoordinate>());
    }
}
