namespace PlayMe.Domain.Platform;

/// <summary>
/// Optional capability for game modules that need a pre-match,
/// simultaneous, secret setup step (Sprint 10 seam C; see
/// docs/games/seabattle.md). A module implements this <em>in addition
/// to</em> <see cref="IGameModule"/>; the platform dispatches by
/// capability (<c>module is ISetupGame</c>), never by <see cref="GameId"/>.
///
/// Rooms for setup games enter <see cref="RoomStatus.SettingUp"/> instead
/// of <see cref="RoomStatus.InProgress"/> once both players are present;
/// each side submits one setup payload (via the <c>SubmitSetup</c> hub
/// method), and when <see cref="IsSetupComplete"/> reports true the room
/// transitions to <see cref="RoomStatus.InProgress"/> and the chess clock
/// starts. The setup phase itself is unclocked — <see cref="SetupBudget"/>
/// bounds it instead. Setup-less games are untouched.
///
/// The platform tracks which roles have committed (one commit per side,
/// final); the module owns payload shape and validation vocabulary.
/// </summary>
public interface ISetupGame
{
    /// <summary>
    /// Per-game setup window, measured from <see cref="RoomStatus.SettingUp"/>
    /// entry. When it elapses with the setup incomplete, the platform
    /// forfeits the uncommitted side (<see cref="Timeout"/>) — or expires
    /// the room when neither side committed.
    /// </summary>
    TimeSpan SetupBudget { get; }

    /// <summary>
    /// Validate one side's setup payload against the current state.
    /// Returns null on success or a module-owned reject key (the same
    /// module ↔ renderer agreement as move reject keys — the platform
    /// never enumerates them).
    /// </summary>
    string? ValidateSetup(IGameState state, string side, GameMove setup);

    /// <summary>
    /// Apply a validated setup payload and return the new state. Called
    /// at most once per side — the platform rejects double commits before
    /// validation.
    /// </summary>
    IGameState ApplySetup(IGameState state, string side, GameMove setup);

    /// <summary>
    /// True when setup is complete and the match can begin. Answered by
    /// the module — not by the platform counting commits — so a future
    /// game with asymmetric setup (only one side places anything) works
    /// without platform changes.
    /// </summary>
    bool IsSetupComplete(IGameState state);
}
