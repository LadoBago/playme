using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using PlayMe.Domain.Games.Checkers;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Per-game rules unit tests for <see cref="CheckersGameModule"/> (Russian
/// draughts). The platform's move pipeline is covered by
/// <c>SubmitMoveHandler</c> tests; these pin the module's rules directly:
/// man and flying-king movement, mandatory capture, chain continuation via
/// <see cref="MoveResult.KeepTurn"/>, the king landing rule, Turkish strike,
/// promotion mid-chain, both win conditions, both draw rules, and the
/// serialized wire shape.
///
/// Conventions:
/// - Cells are row-major on an 8×8 board; row 0 is the top (black's home
///   rank), row 7 the bottom (white's). Dark squares have (row + col) odd.
/// - White moves first and advances toward row 0; black toward row 7.
/// </summary>
public sealed class CheckersGameModuleTests
{
    private const string W = CheckersPieces.WhiteMan;
    private const string B = CheckersPieces.BlackMan;
    private const string WK = CheckersPieces.WhiteKing;
    private const string BK = CheckersPieces.BlackKing;

    private readonly CheckersGameModule _module = new();

    [Fact]
    public void Module_metadata_is_canonical()
    {
        _module.Id.Value.Should().Be("checkers");
        _module.ValidSides.Should().Equal(CheckersSides.White, CheckersSides.Black);
        _module.FirstMoveSide.Should().Be(CheckersSides.White);
        _module.ClockBudgetFor(null).Should().Be(TimeSpan.FromMinutes(10));
        _module.OtherSide(CheckersSides.White).Should().Be(CheckersSides.Black);
        _module.OtherSide(CheckersSides.Black).Should().Be(CheckersSides.White);
    }

    [Fact]
    public void NewMatch_sets_up_the_russian_draughts_position()
    {
        var state = (CheckersState)_module.NewMatch(null);

        state.SideToMove.Should().Be(CheckersSides.White);
        state.WhiteCount.Should().Be(12);
        state.BlackCount.Should().Be(12);
        state.MoveCount.Should().Be(0);
        state.Chain.Should().BeNull();
        state.LastMove.Should().BeNull();
        state.KingMovePlies.Should().Be(0);
        state.History.Should().BeEmpty();
        state.DrawReason.Should().BeNull();
        for (var row = 0; row < CheckersState.Size; row++)
        {
            for (var col = 0; col < CheckersState.Size; col++)
            {
                var expected = !CheckersState.IsDarkSquare(row, col) ? null
                    : row <= 2 ? B
                    : row >= 5 ? W
                    : null;
                state.CellAt(row, col).Should().Be(expected, $"cell ({row},{col})");
            }
        }
        // Four white men on row 5 step forward: the edge man has one target,
        // the other three have two each.
        state.LegalMoves.Should().HaveCount(7);
        state.LegalMoves.Should().AllSatisfy(m =>
        {
            m.Captured.Should().BeNull();
            m.From.Row.Should().Be(5);
            m.To.Row.Should().Be(4);
        });
    }

    [Fact]
    public void Man_steps_one_square_diagonally_forward()
    {
        var state = _module.NewMatch(null);

        var result = _module.ApplyMove(state, CheckersSides.White, Move(5, 2, 4, 3));

        result.Accepted.Should().BeTrue();
        result.KeepTurn.Should().BeFalse();
        result.Ending.Should().BeNull();
        var next = (CheckersState)result.NewState!;
        next.CellAt(5, 2).Should().BeNull();
        next.CellAt(4, 3).Should().Be(W);
        next.SideToMove.Should().Be(CheckersSides.Black);
        next.MoveCount.Should().Be(1);
        next.LastMove.Should().Be(new CheckersMoveInfo(At(5, 2), At(4, 3)));
        next.KingMovePlies.Should().Be(0);
        next.History.Should().HaveCount(1);
        next.LegalMoves.Should().AllSatisfy(m => m.From.Row.Should().Be(2));
    }

    [Theory]
    [InlineData(5, 2, "backward step")]
    [InlineData(4, 5, "sideways")]
    [InlineData(2, 1, "two squares with nothing to jump")]
    [InlineData(3, 3, "light square")]
    public void Man_cannot_move_except_one_forward_diagonal(int toRow, int toCol, string because)
    {
        var state = State(CheckersSides.White, Cells((4, 3, W), (0, 7, B)));

        var result = _module.ApplyMove(state, CheckersSides.White, Move(4, 3, toRow, toCol));

        result.Accepted.Should().BeFalse(because);
        result.RejectKey.Should().Be(CheckersErrors.IllegalMove);
    }

    [Fact]
    public void Moving_an_empty_square_or_the_opponents_piece_is_NotYourPiece()
    {
        var state = _module.NewMatch(null);

        _module.ApplyMove(state, CheckersSides.White, Move(2, 1, 3, 2))
            .RejectKey.Should().Be(CheckersErrors.NotYourPiece);
        _module.ApplyMove(state, CheckersSides.White, Move(4, 1, 3, 2))
            .RejectKey.Should().Be(CheckersErrors.NotYourPiece);
    }

    [Fact]
    public void Coordinates_off_the_board_are_OutOfBounds()
    {
        var state = _module.NewMatch(null);

        _module.ApplyMove(state, CheckersSides.White, Move(5, 0, 4, -1))
            .RejectKey.Should().Be(CheckersErrors.OutOfBounds);
        _module.ApplyMove(state, CheckersSides.White, Move(8, 1, 7, 0))
            .RejectKey.Should().Be(CheckersErrors.OutOfBounds);
    }

    [Fact]
    public void Man_captures_forward_and_the_jumped_piece_is_removed()
    {
        var state = State(CheckersSides.White, Cells((4, 3, W), (3, 2, B), (0, 7, B)));
        state.LegalMoves.Should().Equal(new CheckersMoveInfo(At(4, 3), At(2, 1), At(3, 2)));

        var result = _module.ApplyMove(state, CheckersSides.White, Move(4, 3, 2, 1));

        result.Accepted.Should().BeTrue();
        result.KeepTurn.Should().BeFalse();
        var next = (CheckersState)result.NewState!;
        next.CellAt(4, 3).Should().BeNull();
        next.CellAt(3, 2).Should().BeNull();
        next.CellAt(2, 1).Should().Be(W);
        next.BlackCount.Should().Be(1);
        next.SideToMove.Should().Be(CheckersSides.Black);
        next.MoveCount.Should().Be(1);
        next.Chain.Should().BeNull();
        next.LastMove.Should().Be(new CheckersMoveInfo(At(4, 3), At(2, 1), At(3, 2)));
    }

    [Fact]
    public void Man_captures_backward()
    {
        var state = State(CheckersSides.White, Cells((4, 3, W), (5, 4, B), (0, 7, B)));

        var result = _module.ApplyMove(state, CheckersSides.White, Move(4, 3, 6, 5));

        result.Accepted.Should().BeTrue();
        var next = (CheckersState)result.NewState!;
        next.CellAt(5, 4).Should().BeNull();
        next.CellAt(6, 5).Should().Be(W);
        next.BlackCount.Should().Be(1);
    }

    [Fact]
    public void Capture_is_mandatory_for_the_whole_side()
    {
        var state = State(CheckersSides.White, Cells((4, 3, W), (3, 2, B), (7, 6, W), (0, 7, B)));

        // The capturing piece may not step instead…
        _module.ApplyMove(state, CheckersSides.White, Move(4, 3, 3, 4))
            .RejectKey.Should().Be(CheckersErrors.CaptureMandatory);
        // …and neither may any other piece.
        _module.ApplyMove(state, CheckersSides.White, Move(7, 6, 6, 5))
            .RejectKey.Should().Be(CheckersErrors.CaptureMandatory);
        state.LegalMoves.Should().OnlyContain(m => m.Captured != null);
    }

    [Fact]
    public void Chain_keeps_the_turn_and_removes_captured_pieces_only_when_it_ends()
    {
        var state = State(CheckersSides.White, Cells((6, 1, W), (5, 2, B), (3, 2, B), (7, 6, W), (0, 7, B)));
        state.LegalMoves.Should().Equal(new CheckersMoveInfo(At(6, 1), At(4, 3), At(5, 2)));

        var first = _module.ApplyMove(state, CheckersSides.White, Move(6, 1, 4, 3));

        first.Accepted.Should().BeTrue();
        first.KeepTurn.Should().BeTrue();
        first.Ending.Should().BeNull();
        var mid = (CheckersState)first.NewState!;
        mid.SideToMove.Should().Be(CheckersSides.White);
        mid.MoveCount.Should().Be(0);
        mid.Chain.Should().NotBeNull();
        mid.Chain!.At.Should().Be(At(4, 3));
        mid.Chain.Captured.Should().Equal(At(5, 2));
        mid.CellAt(5, 2).Should().Be(B, "a jumped piece stays on the board until the chain ends");
        mid.CellAt(4, 3).Should().Be(W);
        mid.CellAt(6, 1).Should().BeNull();
        mid.BlackCount.Should().Be(3);
        mid.LastMove.Should().Be(new CheckersMoveInfo(At(6, 1), At(4, 3), At(5, 2)));
        mid.LegalMoves.Should().Equal(new CheckersMoveInfo(At(4, 3), At(2, 1), At(3, 2)));

        var last = _module.ApplyMove(mid, CheckersSides.White, Move(4, 3, 2, 1));

        last.Accepted.Should().BeTrue();
        last.KeepTurn.Should().BeFalse();
        var done = (CheckersState)last.NewState!;
        done.Chain.Should().BeNull();
        done.CellAt(5, 2).Should().BeNull();
        done.CellAt(3, 2).Should().BeNull();
        done.CellAt(2, 1).Should().Be(W);
        done.BlackCount.Should().Be(1);
        done.WhiteCount.Should().Be(2);
        done.SideToMove.Should().Be(CheckersSides.Black);
        done.MoveCount.Should().Be(1);
    }

    [Fact]
    public void Mid_chain_only_the_chaining_piece_may_move_and_only_to_capture()
    {
        var state = State(CheckersSides.White, Cells((6, 1, W), (5, 2, B), (3, 2, B), (7, 6, W), (0, 7, B)));
        var mid = _module.ApplyMove(state, CheckersSides.White, Move(6, 1, 4, 3)).NewState!;

        _module.ApplyMove(mid, CheckersSides.White, Move(7, 6, 6, 5))
            .RejectKey.Should().Be(CheckersErrors.MustContinueCapture, "another piece may not move");
        _module.ApplyMove(mid, CheckersSides.White, Move(4, 3, 3, 4))
            .RejectKey.Should().Be(CheckersErrors.MustContinueCapture, "the chaining piece may not step");
    }

    [Fact]
    public void Turkish_strike_a_pending_piece_cannot_be_jumped_again()
    {
        // After jumping (5,2) the man sits at (4,3); the vacated (6,1) is
        // empty, so geometrically it could jump (5,2) back — but (5,2) is
        // pending and must not be captured twice.
        var state = State(CheckersSides.White, Cells((6, 1, W), (5, 2, B), (3, 2, B), (0, 7, B)));
        var mid = (CheckersState)_module.ApplyMove(state, CheckersSides.White, Move(6, 1, 4, 3)).NewState!;

        mid.LegalMoves.Should().NotContain(m => m.To == At(6, 1));
        _module.ApplyMove(mid, CheckersSides.White, Move(4, 3, 6, 1))
            .RejectKey.Should().Be(CheckersErrors.IllegalMove);
    }

    [Fact]
    public void Flying_king_moves_any_distance_along_a_diagonal()
    {
        var state = State(CheckersSides.White, Cells((7, 0, WK), (0, 1, BK)));
        state.LegalMoves.Should().HaveCount(7);
        state.LegalMoves.Should().AllSatisfy(m => m.Captured.Should().BeNull());

        var result = _module.ApplyMove(state, CheckersSides.White, Move(7, 0, 3, 4));

        result.Accepted.Should().BeTrue();
        var next = (CheckersState)result.NewState!;
        next.CellAt(7, 0).Should().BeNull();
        next.CellAt(3, 4).Should().Be(WK);
        next.KingMovePlies.Should().Be(1, "a king move without capture counts toward the shuffling draw");
        next.History.Should().HaveCount(1);
        _module.ApplyMove(state, CheckersSides.White, Move(7, 0, 5, 0))
            .RejectKey.Should().Be(CheckersErrors.IllegalMove);
    }

    [Fact]
    public void Flying_king_captures_at_distance_and_must_land_where_the_chain_continues()
    {
        // King (6,1) jumps (5,2); landings beyond are (4,3) … (0,7). Only
        // from (3,4) can it go on to capture (2,3), so that is the only
        // legal landing. (1,0) keeps black alive after the chain.
        var state = State(CheckersSides.White, Cells((6, 1, WK), (5, 2, B), (2, 3, B), (1, 0, B)));
        state.LegalMoves.Should().Equal(new CheckersMoveInfo(At(6, 1), At(3, 4), At(5, 2)));

        _module.ApplyMove(state, CheckersSides.White, Move(6, 1, 4, 3))
            .RejectKey.Should().Be(CheckersErrors.MustContinueCapture, "a landing that abandons the chain");
        _module.ApplyMove(state, CheckersSides.White, Move(6, 1, 5, 0))
            .RejectKey.Should().Be(CheckersErrors.CaptureMandatory, "a plain king step while a capture exists");

        var first = _module.ApplyMove(state, CheckersSides.White, Move(6, 1, 3, 4));

        first.KeepTurn.Should().BeTrue();
        var mid = (CheckersState)first.NewState!;
        mid.Chain!.At.Should().Be(At(3, 4));
        // Beyond (2,3) both empty squares are fine (neither continues); the
        // south-west diagonal is blocked by the pending (5,2).
        mid.LegalMoves.Should().BeEquivalentTo(new[]
        {
            new CheckersMoveInfo(At(3, 4), At(1, 2), At(2, 3)),
            new CheckersMoveInfo(At(3, 4), At(0, 1), At(2, 3)),
        });

        var last = _module.ApplyMove(mid, CheckersSides.White, Move(3, 4, 0, 1));

        last.KeepTurn.Should().BeFalse();
        var done = (CheckersState)last.NewState!;
        done.CellAt(5, 2).Should().BeNull();
        done.CellAt(2, 3).Should().BeNull();
        done.CellAt(0, 1).Should().Be(WK);
        done.BlackCount.Should().Be(1);
        done.SideToMove.Should().Be(CheckersSides.Black);
        done.KingMovePlies.Should().Be(0);
    }

    [Fact]
    public void Man_reaching_the_far_rank_by_a_step_becomes_a_king()
    {
        var state = State(CheckersSides.White, Cells((1, 2, W), (3, 0, B)));

        var result = _module.ApplyMove(state, CheckersSides.White, Move(1, 2, 0, 1));

        result.Accepted.Should().BeTrue();
        ((CheckersState)result.NewState!).CellAt(0, 1).Should().Be(WK);
    }

    [Fact]
    public void Man_promoting_mid_chain_continues_capturing_as_a_king()
    {
        // (2,1) jumps (1,2) onto the far rank, promotes, and as a king can
        // take (1,4) landing on (2,5) or (3,6) — a man could only reach (2,5).
        var state = State(CheckersSides.White, Cells((2, 1, W), (1, 2, B), (1, 4, B), (4, 7, B)));

        var first = _module.ApplyMove(state, CheckersSides.White, Move(2, 1, 0, 3));

        first.KeepTurn.Should().BeTrue();
        var mid = (CheckersState)first.NewState!;
        mid.CellAt(0, 3).Should().Be(WK);
        mid.LegalMoves.Should().BeEquivalentTo(new[]
        {
            new CheckersMoveInfo(At(0, 3), At(2, 5), At(1, 4)),
            new CheckersMoveInfo(At(0, 3), At(3, 6), At(1, 4)),
        });

        var done = (CheckersState)_module.ApplyMove(mid, CheckersSides.White, Move(0, 3, 3, 6)).NewState!;

        done.CellAt(3, 6).Should().Be(WK);
        done.CellAt(1, 2).Should().BeNull();
        done.CellAt(1, 4).Should().BeNull();
        done.BlackCount.Should().Be(1);
    }

    [Fact]
    public void Capturing_the_last_piece_wins()
    {
        var state = State(CheckersSides.White, Cells((4, 3, W), (3, 2, B)));

        var result = _module.ApplyMove(state, CheckersSides.White, Move(4, 3, 2, 1));

        result.Accepted.Should().BeTrue();
        result.KeepTurn.Should().BeFalse();
        result.Ending.Should().Be(new Win(CheckersSides.White));
        var done = (CheckersState)result.NewState!;
        done.BlackCount.Should().Be(0);
        done.LegalMoves.Should().BeEmpty();
        done.DrawReason.Should().BeNull();
    }

    [Fact]
    public void Leaving_the_opponent_without_a_legal_move_wins()
    {
        // Black's lone man at (6,1) is hemmed in: both forward squares are
        // occupied and every jump lands off-board or on a white piece.
        var state = State(CheckersSides.White, Cells(
            (7, 0, W), (7, 2, W), (5, 0, W), (5, 2, W), (4, 3, W), (3, 6, W), (6, 1, B)));

        var result = _module.ApplyMove(state, CheckersSides.White, Move(3, 6, 2, 7));

        result.Accepted.Should().BeTrue();
        result.Ending.Should().Be(new Win(CheckersSides.White));
        var done = (CheckersState)result.NewState!;
        done.BlackCount.Should().Be(1);
        done.LegalMoves.Should().BeEmpty();
    }

    [Fact]
    public void Thirty_king_only_plies_without_capture_draw_the_match()
    {
        var state = State(CheckersSides.White, Cells((7, 0, WK), (0, 1, BK)), kingMovePlies: 29);

        var result = _module.ApplyMove(state, CheckersSides.White, Move(7, 0, 6, 1));

        result.Accepted.Should().BeTrue();
        result.Ending.Should().Be(new Draw());
        var done = (CheckersState)result.NewState!;
        done.KingMovePlies.Should().Be(CheckersState.KingMovePlyLimit);
        done.DrawReason.Should().Be(CheckersDrawReasons.KingMoves);
    }

    [Fact]
    public void A_man_move_resets_the_king_move_counter_and_the_repetition_history()
    {
        var state = State(CheckersSides.White, Cells((7, 0, WK), (4, 3, W), (0, 1, BK)), kingMovePlies: 10);
        var kingMoved = (CheckersState)_module.ApplyMove(state, CheckersSides.White, Move(7, 0, 6, 1)).NewState!;
        kingMoved.KingMovePlies.Should().Be(11);
        kingMoved.History.Should().HaveCount(1);
        var blackMoved = (CheckersState)_module.ApplyMove(kingMoved, CheckersSides.Black, Move(0, 1, 1, 0)).NewState!;
        blackMoved.KingMovePlies.Should().Be(12);
        blackMoved.History.Should().HaveCount(2);

        var manMoved = (CheckersState)_module.ApplyMove(blackMoved, CheckersSides.White, Move(4, 3, 3, 4)).NewState!;

        manMoved.KingMovePlies.Should().Be(0);
        manMoved.History.Should().HaveCount(1, "an irreversible move starts a fresh history");
    }

    [Fact]
    public void Third_occurrence_of_a_position_draws_the_match()
    {
        // Two kings shuffle on diagonals that never meet. The position after
        // white's first move recurs on plies 5 and 9 — the third time ends it.
        IGameState state = State(CheckersSides.White, Cells((7, 0, WK), (0, 1, BK)));
        var plies = new[]
        {
            (CheckersSides.White, Move(7, 0, 6, 1)),
            (CheckersSides.Black, Move(0, 1, 1, 0)),
            (CheckersSides.White, Move(6, 1, 7, 0)),
            (CheckersSides.Black, Move(1, 0, 0, 1)),
            (CheckersSides.White, Move(7, 0, 6, 1)),
            (CheckersSides.Black, Move(0, 1, 1, 0)),
            (CheckersSides.White, Move(6, 1, 7, 0)),
            (CheckersSides.Black, Move(1, 0, 0, 1)),
        };
        foreach (var (side, move) in plies)
        {
            var step = _module.ApplyMove(state, side, move);
            step.Accepted.Should().BeTrue();
            step.Ending.Should().BeNull();
            state = step.NewState!;
        }

        var result = _module.ApplyMove(state, CheckersSides.White, Move(7, 0, 6, 1));

        result.Accepted.Should().BeTrue();
        result.Ending.Should().Be(new Draw());
        var done = (CheckersState)result.NewState!;
        done.DrawReason.Should().Be(CheckersDrawReasons.Repetition);
        done.History.Should().HaveCount(9);
    }

    [Fact]
    public void Serialize_and_Deserialize_round_trip_preserves_an_open_chain()
    {
        var state = State(CheckersSides.White, Cells((6, 1, W), (5, 2, B), (3, 2, B), (7, 6, W), (0, 7, B)));
        var mid = (CheckersState)_module.ApplyMove(state, CheckersSides.White, Move(6, 1, 4, 3)).NewState!;

        var json = _module.Serialize(mid);
        var restored = (CheckersState)_module.Deserialize(json);

        restored.Cells.Should().Equal(mid.Cells);
        restored.SideToMove.Should().Be(mid.SideToMove);
        restored.MoveCount.Should().Be(mid.MoveCount);
        restored.LastMove.Should().Be(mid.LastMove);
        restored.Chain.Should().NotBeNull();
        restored.Chain!.At.Should().Be(mid.Chain!.At);
        restored.Chain.Captured.Should().Equal(mid.Chain.Captured);
        restored.LegalMoves.Should().BeEquivalentTo(mid.LegalMoves);
        restored.WhiteCount.Should().Be(mid.WhiteCount);
        restored.BlackCount.Should().Be(mid.BlackCount);
        restored.KingMovePlies.Should().Be(mid.KingMovePlies);
        restored.History.Should().Equal(mid.History);
        restored.DrawReason.Should().BeNull();
    }

    [Fact]
    public void Serialized_state_uses_the_documented_wire_shape()
    {
        var json = _module.Serialize(_module.NewMatch(null));

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.GetProperty("cells").GetArrayLength().Should().Be(64);
        root.GetProperty("cells")[1].GetString().Should().Be("black");
        root.GetProperty("sideToMove").GetString().Should().Be("white");
        root.GetProperty("moveCount").GetInt32().Should().Be(0);
        root.GetProperty("legalMoves").GetArrayLength().Should().Be(7);
        root.GetProperty("legalMoves")[0].GetProperty("from").GetProperty("row").GetInt32().Should().Be(5);
        root.GetProperty("whiteCount").GetInt32().Should().Be(12);
        root.GetProperty("blackCount").GetInt32().Should().Be(12);
        root.GetProperty("kingMovePlies").GetInt32().Should().Be(0);
        root.GetProperty("history").GetArrayLength().Should().Be(0);
        root.TryGetProperty("chain", out _).Should().BeFalse("nulls are omitted");
        root.TryGetProperty("lastMove", out _).Should().BeFalse();
        root.TryGetProperty("drawReason", out _).Should().BeFalse();
    }

    [Fact]
    public void Deserialize_recomputes_legalMoves_instead_of_trusting_the_blob()
    {
        var node = JsonNode.Parse(_module.Serialize(_module.NewMatch(null)))!.AsObject();
        node["legalMoves"] = new JsonArray();

        var restored = (CheckersState)_module.Deserialize(node.ToJsonString());

        restored.LegalMoves.Should().HaveCount(7);
    }

    [Theory]
    [InlineData("""{"cells":[null,null,null],"sideToMove":"white"}""", "wrong cell count")]
    [InlineData("""{"cells":[],"sideToMove":"white"}""", "empty cells")]
    [InlineData("not json", "garbage")]
    public void Deserialize_rejects_malformed_blobs(string blob, string because)
    {
        var act = () => _module.Deserialize(blob);

        act.Should().Throw<ArgumentException>(because);
    }

    [Fact]
    public void Deserialize_rejects_unknown_side_and_unknown_piece_tokens()
    {
        var node = JsonNode.Parse(_module.Serialize(_module.NewMatch(null)))!.AsObject();
        node["sideToMove"] = "red";
        var badSide = () => _module.Deserialize(node.ToJsonString());
        badSide.Should().Throw<ArgumentException>();

        node["sideToMove"] = "white";
        node["cells"]![1] = "queen";
        var badPiece = () => _module.Deserialize(node.ToJsonString());
        badPiece.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ApplyMove_rejects_foreign_state_and_move_types()
    {
        var state = _module.NewMatch(null);

        var badMove = () => _module.ApplyMove(state, CheckersSides.White, new ForeignMove());
        badMove.Should().Throw<ArgumentException>();
        var badState = () => _module.ApplyMove(new ForeignState(), CheckersSides.White, Move(5, 0, 4, 1));
        badState.Should().Throw<ArgumentException>();
        var badSide = () => _module.ApplyMove(state, "red", Move(5, 0, 4, 1));
        badSide.Should().Throw<ArgumentException>();
    }

    // --- Helpers -----------------------------------------------------------

    private static CheckersCoordinate At(int row, int col) => new(row, col);

    private static CheckersMove Move(int fromRow, int fromCol, int toRow, int toCol) =>
        new(At(fromRow, fromCol), At(toRow, toCol));

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
    /// server-computed list, exactly as a Redis-loaded state would carry.
    /// </summary>
    private CheckersState State(
        string side, string?[] cells, CheckersChain? chain = null, int kingMovePlies = 0)
    {
        var raw = new CheckersState(
            cells, side, moveCount: 0, lastMove: null, chain, Array.Empty<CheckersMoveInfo>(),
            kingMovePlies, Array.Empty<string>());
        return (CheckersState)_module.Deserialize(_module.Serialize(raw));
    }

    private sealed record ForeignMove : GameMove;

    private sealed class ForeignState : IGameState
    {
    }
}
