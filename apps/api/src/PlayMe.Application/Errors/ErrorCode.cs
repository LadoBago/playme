namespace PlayMe.Application.Errors;

/// <summary>
/// Application-level error codes returned by handlers (CLAUDE.md §3). Each
/// value is tagged with its i18n key; the API translates to ProblemDetails
/// at the boundary, and the web client maps the key to a localized message.
///
/// Sprint 1 covers only the codes its handlers can actually emit. Later
/// sprints add codes (timeouts, rematches, disconnect grace) — extending
/// this enum and adding the matching translation keys are paired changes.
/// </summary>
public enum ErrorCode
{
    // --- Validation ---
    [I18nKey("errors.validation.displayName")]
    ValidationDisplayName,

    [I18nKey("errors.validation.move")]
    ValidationMove,

    // --- Configuration (room creation) ---
    [I18nKey("errors.config.invalidGameId")]
    ConfigInvalidGameId,

    [I18nKey("errors.config.invalidSideSelectionMode")]
    ConfigInvalidSideSelectionMode,

    [I18nKey("errors.config.invalidHostSide")]
    ConfigInvalidHostSide,

    // --- Join flow ---
    [I18nKey("errors.join.sideNotAllowed")]
    JoinSideNotAllowed,

    [I18nKey("errors.join.sidePickRequired")]
    JoinSidePickRequired,

    [I18nKey("errors.join.invalidSide")]
    JoinInvalidSide,

    // --- Room state ---
    [I18nKey("errors.room.notFound")]
    RoomNotFound,

    [I18nKey("errors.room.alreadyJoined")]
    RoomAlreadyJoined,

    [I18nKey("errors.room.notJoinable")]
    RoomNotJoinable,

    [I18nKey("errors.room.busy")]
    RoomBusy,

    // --- Move-time ---
    [I18nKey("errors.move.illegalCell")]
    MoveIllegalCell,

    [I18nKey("errors.move.cellOccupied")]
    MoveCellOccupied,

    [I18nKey("errors.move.notYourTurn")]
    MoveNotYourTurn,

    [I18nKey("errors.move.matchNotInProgress")]
    MoveMatchNotInProgress,

    // --- Session / authorization ---
    [I18nKey("errors.session.unauthorized")]
    SessionUnauthorized,
}
