using System.Text.Json;
using FluentAssertions;
using PlayMe.Domain.Games.SeaBattle;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Sea Battle rules per docs/games/seabattle.md: 10×10, fleet
/// 1×4 + 2×3 + 3×2 + 4×1 with the no-touch rule (not even diagonally),
/// hit/sunk = extra shot (seam B), win on the 20th fleet cell, draw
/// impossible. Plus the seam A projection contract (own fleet visible,
/// opponent's surviving fleet never on the live wire) and the seam C
/// setup hooks.
/// </summary>
public sealed class SeaBattleGameModuleTests
{
    private static readonly SeaBattleGameModule Module = new();

    /// <summary>
    /// Canonical legal fleet used across the tests:
    /// row 0: 4-decker x0–3, 3-decker x5–7, 1-decker x9
    /// row 2: 3-decker x0–2, 2-decker x4–5, 2-decker x7–8
    /// row 4: 2-decker x0–1, 1-deckers at x3, x5, x7
    /// Rows 0/2/4 leave a full empty row between ships, and every
    /// same-row gap is ≥ 2 cells, so nothing touches.
    /// </summary>
    private static List<SeaBattleShip> LegalFleet() => new()
    {
        new SeaBattleShip(0, 0, 4, Horizontal: true),
        new SeaBattleShip(5, 0, 3, Horizontal: true),
        new SeaBattleShip(9, 0, 1, Horizontal: true),
        new SeaBattleShip(0, 2, 3, Horizontal: true),
        new SeaBattleShip(4, 2, 2, Horizontal: true),
        new SeaBattleShip(7, 2, 2, Horizontal: true),
        new SeaBattleShip(0, 4, 2, Horizontal: true),
        new SeaBattleShip(3, 4, 1, Horizontal: true),
        new SeaBattleShip(5, 4, 1, Horizontal: true),
        new SeaBattleShip(7, 4, 1, Horizontal: true),
    };

    private static SeaBattleState BattleState()
    {
        var afterFirst = (SeaBattleState)Module.ApplySetup(
            SeaBattleState.Empty, SeaBattleSides.First,
            new SeaBattleFleetPlacement(LegalFleet()));
        return (SeaBattleState)Module.ApplySetup(
            afterFirst, SeaBattleSides.Second,
            new SeaBattleFleetPlacement(LegalFleet()));
    }

    private static JsonElement Projection(IGameState state, string? viewerSide) =>
        JsonDocument.Parse(Module.SerializeFor(state, viewerSide)).RootElement;

    // --- Setup validation ----------------------------------------------------

    [Fact]
    public void Legal_fleet_validates_and_completes_setup_for_both_sides()
    {
        var placement = new SeaBattleFleetPlacement(LegalFleet());

        Module.ValidateSetup(SeaBattleState.Empty, SeaBattleSides.First, placement)
            .Should().BeNull();

        var afterFirst = Module.ApplySetup(SeaBattleState.Empty, SeaBattleSides.First, placement);
        Module.IsSetupComplete(afterFirst).Should().BeFalse();

        var afterBoth = Module.ApplySetup(afterFirst, SeaBattleSides.Second, placement);
        Module.IsSetupComplete(afterBoth).Should().BeTrue();
    }

    [Fact]
    public void Fleet_with_wrong_ship_counts_is_rejected()
    {
        var ships = LegalFleet();
        // Swap a 1-decker for an extra 2-decker: counts become 2×1, 4×2.
        ships[9] = new SeaBattleShip(7, 4, 2, Horizontal: true);

        Module.ValidateSetup(
                SeaBattleState.Empty, SeaBattleSides.First, new SeaBattleFleetPlacement(ships))
            .Should().Be(SeaBattleErrors.InvalidFleet);
    }

    [Fact]
    public void Fleet_with_too_few_ships_is_rejected()
    {
        var ships = LegalFleet();
        ships.RemoveAt(9);

        Module.ValidateSetup(
                SeaBattleState.Empty, SeaBattleSides.First, new SeaBattleFleetPlacement(ships))
            .Should().Be(SeaBattleErrors.InvalidFleet);
    }

    [Fact]
    public void Ships_touching_orthogonally_are_rejected()
    {
        var ships = LegalFleet();
        // Move the row-4 1-decker at x3 directly right of the 2-decker
        // ending at x1 → cells (1,4) and (2,4) are adjacent.
        ships[7] = new SeaBattleShip(2, 4, 1, Horizontal: true);

        Module.ValidateSetup(
                SeaBattleState.Empty, SeaBattleSides.First, new SeaBattleFleetPlacement(ships))
            .Should().Be(SeaBattleErrors.InvalidFleet);
    }

    [Fact]
    public void Ships_touching_diagonally_are_rejected()
    {
        var ships = LegalFleet();
        // Move a row-4 1-decker to (4,3): diagonal neighbor of the
        // 2-decker cell (4,2) and of (5,2).
        ships[7] = new SeaBattleShip(4, 3, 1, Horizontal: true);

        Module.ValidateSetup(
                SeaBattleState.Empty, SeaBattleSides.First, new SeaBattleFleetPlacement(ships))
            .Should().Be(SeaBattleErrors.InvalidFleet);
    }

    [Fact]
    public void Overlapping_ships_are_rejected()
    {
        var ships = LegalFleet();
        ships[7] = new SeaBattleShip(0, 4, 1, Horizontal: true); // on the 2-decker

        Module.ValidateSetup(
                SeaBattleState.Empty, SeaBattleSides.First, new SeaBattleFleetPlacement(ships))
            .Should().Be(SeaBattleErrors.InvalidFleet);
    }

    [Fact]
    public void Out_of_bounds_ship_is_rejected()
    {
        var ships = LegalFleet();
        // Move the 4-decker to x7..x10 on an otherwise-empty row — the
        // composition stays legal, only the last cell leaves the grid.
        ships[0] = new SeaBattleShip(7, 9, 4, Horizontal: true);

        Module.ValidateSetup(
                SeaBattleState.Empty, SeaBattleSides.First, new SeaBattleFleetPlacement(ships))
            .Should().Be(SeaBattleErrors.InvalidFleet);
    }

    [Fact]
    public void Shot_payload_is_not_a_valid_setup()
    {
        Module.ValidateSetup(SeaBattleState.Empty, SeaBattleSides.First, new SeaBattleShot(0, 0))
            .Should().Be(SeaBattleErrors.ValidationMove);
    }

    // --- Shots --------------------------------------------------------------

    [Fact]
    public void Miss_is_accepted_without_turn_retention()
    {
        var result = Module.ApplyMove(BattleState(), SeaBattleSides.First, new SeaBattleShot(0, 9));

        result.Accepted.Should().BeTrue();
        result.KeepTurn.Should().BeFalse();
        result.Ending.Should().BeNull();
    }

    [Fact]
    public void Hit_retains_the_turn()
    {
        // (0,0) is a 4-decker cell of second's fleet.
        var result = Module.ApplyMove(BattleState(), SeaBattleSides.First, new SeaBattleShot(0, 0));

        result.Accepted.Should().BeTrue();
        result.KeepTurn.Should().BeTrue();
        result.Ending.Should().BeNull();
    }

    [Fact]
    public void Sinking_a_ship_retains_the_turn_and_marks_it_sunk_in_the_projection()
    {
        // Sink second's 1-decker at (9,0).
        var result = Module.ApplyMove(BattleState(), SeaBattleSides.First, new SeaBattleShot(9, 0));
        result.Accepted.Should().BeTrue();
        result.KeepTurn.Should().BeTrue();

        var view = Projection(result.NewState!, SeaBattleSides.First);
        view.GetProperty("shots").GetProperty("first")[0]
            .GetProperty("result").GetString().Should().Be("sunk");
        view.GetProperty("sunk").GetProperty("first").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public void Duplicate_shot_is_rejected_and_out_of_bounds_shot_is_rejected()
    {
        var afterShot = Module.ApplyMove(
            BattleState(), SeaBattleSides.First, new SeaBattleShot(0, 9));

        Module.ApplyMove(afterShot.NewState!, SeaBattleSides.First, new SeaBattleShot(0, 9))
            .RejectKey.Should().Be(SeaBattleErrors.AlreadyShot);
        Module.ApplyMove(BattleState(), SeaBattleSides.First, new SeaBattleShot(10, 0))
            .RejectKey.Should().Be(SeaBattleErrors.OutOfBounds);
        Module.ApplyMove(BattleState(), SeaBattleSides.First, new SeaBattleShot(0, -1))
            .RejectKey.Should().Be(SeaBattleErrors.OutOfBounds);
    }

    [Fact]
    public void Fleet_payload_submitted_as_a_move_is_rejected()
    {
        Module.ApplyMove(
                BattleState(), SeaBattleSides.First,
                new SeaBattleFleetPlacement(LegalFleet()))
            .RejectKey.Should().Be(SeaBattleErrors.ValidationMove);
    }

    [Fact]
    public void Deduced_empty_cell_is_a_legal_wasted_miss()
    {
        // Sink the 1-decker at (9,0), then fire at its neighbor (9,1) —
        // guaranteed water by the no-touch rule, but a legal miss.
        var sunk = Module.ApplyMove(BattleState(), SeaBattleSides.First, new SeaBattleShot(9, 0));
        var neighbor = Module.ApplyMove(
            sunk.NewState!, SeaBattleSides.First, new SeaBattleShot(9, 1));

        neighbor.Accepted.Should().BeTrue();
        neighbor.KeepTurn.Should().BeFalse();
    }

    [Fact]
    public void Hitting_all_twenty_cells_wins_immediately()
    {
        IGameState state = BattleState();
        var fleetCells = LegalFleet().SelectMany(s => s.Cells()).ToArray();
        fleetCells.Should().HaveCount(SeaBattleState.FleetCellCount);

        MoveResult last = default!;
        foreach (var cell in fleetCells)
        {
            last = Module.ApplyMove(state, SeaBattleSides.First, new SeaBattleShot(cell.X, cell.Y));
            last.Accepted.Should().BeTrue();
            last.KeepTurn.Should().BeTrue("every fleet shot is a hit");
            state = last.NewState!;
        }

        last.Ending.Should().BeOfType<Win>()
            .Which.WinningSide.Should().Be(SeaBattleSides.First);
    }

    // --- Projection (seam A) ----------------------------------------------------

    [Fact]
    public void Player_projection_contains_own_fleet_and_never_the_opponents()
    {
        var afterHit = Module.ApplyMove(
            BattleState(), SeaBattleSides.First, new SeaBattleShot(0, 0));

        var firstView = Projection(afterHit.NewState!, SeaBattleSides.First);
        firstView.GetProperty("viewerSide").GetString().Should().Be("first");
        firstView.GetProperty("yourFleet").GetArrayLength().Should().Be(10);
        firstView.GetProperty("phase").GetString().Should().Be("battle");

        // The raw projection text must not contain any fleet data beyond
        // yourFleet — i.e. the opponent's surviving ships are absent. The
        // only ship coordinates allowed are the viewer's own fleet and
        // fully sunk opponent ships (none here).
        var raw = Module.SerializeFor(afterHit.NewState!, SeaBattleSides.Second);
        var secondView = JsonDocument.Parse(raw).RootElement;
        secondView.GetProperty("yourFleet").GetArrayLength().Should().Be(10);
        secondView.GetProperty("sunk").GetProperty("first").GetArrayLength().Should().Be(0);
        secondView.GetProperty("sunk").GetProperty("second").GetArrayLength().Should().Be(0);
        secondView.TryGetProperty("firstFleet", out _).Should().BeFalse();

        // The hit's result is public knowledge for both viewers.
        secondView.GetProperty("shots").GetProperty("first")[0]
            .GetProperty("result").GetString().Should().Be("hit");
    }

    [Fact]
    public void Public_projection_has_no_fleets_at_all()
    {
        var afterHit = Module.ApplyMove(
            BattleState(), SeaBattleSides.First, new SeaBattleShot(0, 0));

        var publicView = Projection(afterHit.NewState!, viewerSide: null);
        publicView.TryGetProperty("yourFleet", out _).Should().BeFalse(
            "null fields are omitted from the JSON");
        publicView.TryGetProperty("viewerSide", out _).Should().BeFalse();
        publicView.GetProperty("shots").GetProperty("first").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public void Setup_phase_projection_reports_setup_and_own_fleet_once_placed()
    {
        var afterFirst = Module.ApplySetup(
            SeaBattleState.Empty, SeaBattleSides.First,
            new SeaBattleFleetPlacement(LegalFleet()));

        var firstView = Projection(afterFirst, SeaBattleSides.First);
        firstView.GetProperty("phase").GetString().Should().Be("setup");
        firstView.GetProperty("yourFleet").GetArrayLength().Should().Be(10);

        var secondView = Projection(afterFirst, SeaBattleSides.Second);
        secondView.GetProperty("phase").GetString().Should().Be("setup");
        secondView.TryGetProperty("yourFleet", out _).Should().BeFalse(
            "second hasn't placed yet and must not see first's fleet");
    }

    // --- Serialization -----------------------------------------------------------

    [Fact]
    public void Full_state_round_trips_through_serialize_and_deserialize()
    {
        var afterShots = Module.ApplyMove(
            BattleState(), SeaBattleSides.First, new SeaBattleShot(0, 0));
        var state = (SeaBattleState)Module.ApplyMove(
            afterShots.NewState!, SeaBattleSides.First, new SeaBattleShot(9, 9)).NewState!;

        var roundTripped = (SeaBattleState)Module.Deserialize(Module.Serialize(state));

        roundTripped.FirstFleet.Should().BeEquivalentTo(state.FirstFleet);
        roundTripped.SecondFleet.Should().BeEquivalentTo(state.SecondFleet);
        roundTripped.ShotsByFirst.Should().Equal(state.ShotsByFirst);
        roundTripped.ShotsBySecond.Should().Equal(state.ShotsBySecond);
    }

    [Fact]
    public void Partial_setup_state_round_trips()
    {
        var afterFirst = (SeaBattleState)Module.ApplySetup(
            SeaBattleState.Empty, SeaBattleSides.First,
            new SeaBattleFleetPlacement(LegalFleet()));

        var roundTripped = (SeaBattleState)Module.Deserialize(Module.Serialize(afterFirst));

        roundTripped.FirstFleet.Should().BeEquivalentTo(afterFirst.FirstFleet);
        roundTripped.SecondFleet.Should().BeNull();
        roundTripped.SetupComplete.Should().BeFalse();
    }

    [Fact]
    public void Garbage_state_blob_throws_argument_exception()
    {
        var act = () => Module.Deserialize("not json");
        act.Should().Throw<ArgumentException>();
    }

    // --- Module facts -------------------------------------------------------------

    [Fact]
    public void Module_facts_match_the_canonical_spec()
    {
        Module.Id.Value.Should().Be("seabattle");
        Module.ValidSides.Should().Equal("first", "second");
        Module.FirstMoveSide.Should().Be("first");
        Module.DefaultClockBudget.Should().Be(TimeSpan.FromMinutes(10));
        Module.SetupBudget.Should().Be(TimeSpan.FromMinutes(2));
        Module.OtherSide("first").Should().Be("second");
        Module.ValidateOptions(null).Should().BeNull();
        Module.ValidateOptions(JsonDocument.Parse("{}").RootElement)
            .Should().Be("errors.config.invalidGameOptions");
    }
}
