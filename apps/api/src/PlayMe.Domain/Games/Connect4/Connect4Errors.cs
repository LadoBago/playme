namespace PlayMe.Domain.Games.Connect4;

/// <summary>
/// Reject-key vocabulary owned by the Connect 4 module. These strings are
/// the contract between this module and the Connect 4 web renderer
/// (CLAUDE.md §7 "Platform thinness" — the platform never enumerates them).
/// The web side maps each key to a localized message via the shared i18n
/// catalog.
/// </summary>
public static class Connect4Errors
{
    /// <summary>Move payload malformed (missing or non-numeric `column`).</summary>
    public const string ValidationMove = "errors.validation.move";

    /// <summary>Column index out of range (not 0..6).</summary>
    public const string IllegalColumn = "errors.move.illegalColumn";

    /// <summary>Target column has no empty cells.</summary>
    public const string ColumnFull = "errors.move.columnFull";
}
