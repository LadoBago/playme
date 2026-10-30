namespace PlayMe.Domain.Games.Checkers;

/// <summary>
/// Reject-key vocabulary owned by the Checkers module. These strings are
/// the contract between this module and the Checkers web renderer
/// (CLAUDE.md §7 "Platform thinness" — the platform never enumerates them).
/// The web side maps each key to a localized message via the shared i18n
/// catalog.
/// </summary>
public static class CheckersErrors
{
    /// <summary>Move payload malformed (missing or non-numeric coordinates).</summary>
    public const string ValidationMove = "errors.validation.move";

    /// <summary>Origin or destination outside the 0..7 × 0..7 board.</summary>
    public const string OutOfBounds = "errors.move.outOfBounds";

    /// <summary>Origin square is empty or holds the opponent's piece.</summary>
    public const string NotYourPiece = "errors.move.notYourPiece";

    /// <summary>Destination not reachable by that piece (wrong direction,
    /// not diagonal, occupied, blocked path, nothing to jump).</summary>
    public const string IllegalMove = "errors.move.illegalMove";

    /// <summary>A non-capturing move while the side to move has a capture
    /// available — capturing is mandatory.</summary>
    public const string CaptureMandatory = "errors.move.captureMandatory";

    /// <summary>Mid-chain: a move by a different piece, a non-capturing move
    /// by the chaining piece, or a king landing from which the chain could
    /// have continued but would not.</summary>
    public const string MustContinueCapture = "errors.move.mustContinueCapture";
}
