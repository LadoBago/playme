namespace PlayMe.Application.Errors;

/// <summary>
/// Platform-owned error keys (CLAUDE.md §7 "Platform thinness"). Returned by
/// platform handlers via <see cref="AppResult{T}.Fail(string, string?)"/> and
/// used as the i18n key on the web — the catalog entries in
/// <c>packages/shared/src/i18n/{en,ka}.ts</c> are keyed by these same
/// strings.
///
/// Game modules MUST NOT add entries here. Per-game reject keys are an
/// agreement between the per-game server module and the per-game web
/// renderer; the platform never enumerates them.
/// </summary>
public static class PlatformErrors
{
    // --- Validation ---
    public const string ValidationDisplayName = "errors.validation.displayName";
    public const string ValidationMove = "errors.validation.move";

    // --- Configuration (room creation) ---
    public const string ConfigInvalidGameId = "errors.config.invalidGameId";
    public const string ConfigInvalidSideSelectionMode = "errors.config.invalidSideSelectionMode";
    public const string ConfigInvalidHostSide = "errors.config.invalidHostSide";

    // --- Join flow ---
    public const string JoinSideNotAllowed = "errors.join.sideNotAllowed";
    public const string JoinSidePickRequired = "errors.join.sidePickRequired";
    public const string JoinInvalidSide = "errors.join.invalidSide";

    // --- Room state ---
    public const string RoomNotFound = "errors.room.notFound";
    public const string RoomAlreadyJoined = "errors.room.alreadyJoined";
    public const string RoomNotJoinable = "errors.room.notJoinable";
    public const string RoomBusy = "errors.room.busy";

    // --- Move-time ---
    public const string MoveIllegalCell = "errors.move.illegalCell";
    public const string MoveCellOccupied = "errors.move.cellOccupied";
    public const string MoveNotYourTurn = "errors.move.notYourTurn";
    public const string MoveMatchNotInProgress = "errors.move.matchNotInProgress";

    // --- Session / authorization ---
    public const string SessionUnauthorized = "errors.session.unauthorized";
}
