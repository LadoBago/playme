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

    // --- Configuration (room creation) ---
    public const string ConfigInvalidGameId = "errors.config.invalidGameId";
    public const string ConfigInvalidSideSelectionMode = "errors.config.invalidSideSelectionMode";
    public const string ConfigInvalidHostSide = "errors.config.invalidHostSide";
    public const string ConfigInvalidGameOptions = "errors.config.invalidGameOptions";

    // --- Join flow ---
    public const string JoinSideNotAllowed = "errors.join.sideNotAllowed";
    public const string JoinSidePickRequired = "errors.join.sidePickRequired";
    public const string JoinInvalidSide = "errors.join.invalidSide";

    // --- Room state ---
    public const string RoomNotFound = "errors.room.notFound";
    public const string RoomAlreadyJoined = "errors.room.alreadyJoined";
    public const string RoomNotJoinable = "errors.room.notJoinable";
    public const string RoomBusy = "errors.room.busy";
    public const string ExitNotAllowed = "errors.exit.notAllowed";

    // --- Rematch handshake (docs/platform.md §1 #10) ---
    public const string RematchInvalidState = "errors.rematch.invalidState";
    public const string RematchNotResponder = "errors.rematch.notResponder";

    // --- Move-time (platform-level only — per-game reject keys live in
    //     each module's own constants class, e.g. TicTacToeErrors) ---
    public const string MoveNotYourTurn = "errors.move.notYourTurn";
    public const string MoveMatchNotInProgress = "errors.move.matchNotInProgress";

    // --- Setup phase (Sprint 10 seam C). One commit per side is a
    //     platform rule, so its rejection key is platform-owned; payload
    //     validation failures use the module's own reject keys. ---
    public const string SetupNotInSetup = "errors.setup.notInSetup";
    public const string SetupAlreadyCommitted = "errors.setup.alreadyCommitted";

    // --- Emote (in-match reactions). The allowlist itself lives in
    //     Domain/Platform/Emote.cs; an unknown id is a client/contract
    //     violation, so it surfaces rather than being dropped silently. ---
    public const string EmoteUnknown = "errors.emote.unknown";

    // --- Session / authorization ---
    public const string SessionUnauthorized = "errors.session.unauthorized";

    // --- Rate limiting (docs/security.md §5) ---
    public const string RateExceeded = "errors.rate.exceeded";
}
