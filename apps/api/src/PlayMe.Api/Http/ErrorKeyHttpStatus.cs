using PlayMe.Application.Errors;

namespace PlayMe.Api.Http;

/// <summary>
/// Maps a platform error key (see <see cref="PlatformErrors"/>) to its HTTP
/// status. Lives in the API layer because HTTP status codes are a transport
/// concern; Application doesn't care. Unknown keys fall through to 400 — that
/// covers per-game reject keys returned by a game module (CLAUDE.md §7
/// "Platform thinness"), which are always client-side input faults.
/// </summary>
public static class ErrorKeyHttpStatus
{
    public static int ToHttpStatus(this string key) => key switch
    {
        // 400 — bad input or move-time validation
        PlatformErrors.ValidationDisplayName => StatusCodes.Status400BadRequest,
        PlatformErrors.ValidationMove => StatusCodes.Status400BadRequest,
        PlatformErrors.ConfigInvalidGameId => StatusCodes.Status400BadRequest,
        PlatformErrors.ConfigInvalidSideSelectionMode => StatusCodes.Status400BadRequest,
        PlatformErrors.ConfigInvalidHostSide => StatusCodes.Status400BadRequest,
        PlatformErrors.JoinSideNotAllowed => StatusCodes.Status400BadRequest,
        PlatformErrors.JoinSidePickRequired => StatusCodes.Status400BadRequest,
        PlatformErrors.JoinInvalidSide => StatusCodes.Status400BadRequest,
        PlatformErrors.MoveIllegalCell => StatusCodes.Status400BadRequest,
        PlatformErrors.MoveCellOccupied => StatusCodes.Status400BadRequest,
        PlatformErrors.MoveNotYourTurn => StatusCodes.Status400BadRequest,

        // 401 — cookie missing, tampered, expired, or playerId mismatch
        PlatformErrors.SessionUnauthorized => StatusCodes.Status401Unauthorized,

        // 404 — opaque code didn't resolve to a live room
        PlatformErrors.RoomNotFound => StatusCodes.Status404NotFound,

        // 409 — room state precludes the request (seat taken, not joinable,
        // match not in progress)
        PlatformErrors.RoomAlreadyJoined => StatusCodes.Status409Conflict,
        PlatformErrors.RoomNotJoinable => StatusCodes.Status409Conflict,
        PlatformErrors.MoveMatchNotInProgress => StatusCodes.Status409Conflict,

        // 429 — lock contention; retry shortly
        PlatformErrors.RoomBusy => StatusCodes.Status429TooManyRequests,

        // Per-game reject keys (module-supplied; not enumerated by the
        // platform) and any unrecognized key — 400 by default.
        _ => StatusCodes.Status400BadRequest,
    };
}
