namespace PlayMe.Domain.Games.SeaBattle;

/// <summary>
/// Reject-key vocabulary owned by the Sea Battle module. These strings are
/// the contract between this module and the Sea Battle web renderer
/// (CLAUDE.md §7 "Platform thinness" — the platform never enumerates them).
/// The web side maps each key to a localized message via the shared i18n
/// catalog. Platform-owned setup keys (<c>errors.setup.notInSetup</c>,
/// <c>errors.setup.alreadyCommitted</c>) are rejected by the platform
/// before this module is consulted.
/// </summary>
public static class SeaBattleErrors
{
    /// <summary>Move payload malformed (neither a shot nor a fleet placement,
    /// or the wrong kind for the current phase).</summary>
    public const string ValidationMove = "errors.validation.move";

    /// <summary>Shot (x, y) outside the 0..9 × 0..9 grid.</summary>
    public const string OutOfBounds = "errors.move.outOfBounds";

    /// <summary>Shot at a cell the shooter has already fired at.</summary>
    public const string AlreadyShot = "errors.move.alreadyShot";

    /// <summary>
    /// Fleet fails composition rules: not exactly 1×4 + 2×3 + 3×2 + 4×1
    /// straight ships, inside the grid, with no two ships touching — not
    /// even diagonally. One key for every violation: legal clients generate
    /// fleets locally, so an invalid commit is a bug or tampering, not a
    /// user mistake worth distinguishing.
    /// </summary>
    public const string InvalidFleet = "errors.setup.invalidFleet";
}
