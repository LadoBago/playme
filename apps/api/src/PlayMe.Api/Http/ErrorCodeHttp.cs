using PlayMe.Application.Errors;

namespace PlayMe.Api.Http;

/// <summary>
/// Maps an <see cref="ErrorCode"/> to its HTTP status. Lives in the API
/// layer because HTTP status codes are a transport concern; Application
/// doesn't care.
/// </summary>
public static class ErrorCodeHttp
{
    public static int ToHttpStatus(this ErrorCode code) => code switch
    {
        // 400 — bad input or move-time validation
        ErrorCode.ValidationDisplayName => StatusCodes.Status400BadRequest,
        ErrorCode.ValidationMove => StatusCodes.Status400BadRequest,
        ErrorCode.ConfigInvalidGameId => StatusCodes.Status400BadRequest,
        ErrorCode.ConfigInvalidSideSelectionMode => StatusCodes.Status400BadRequest,
        ErrorCode.ConfigInvalidHostSide => StatusCodes.Status400BadRequest,
        ErrorCode.JoinSideNotAllowed => StatusCodes.Status400BadRequest,
        ErrorCode.JoinSidePickRequired => StatusCodes.Status400BadRequest,
        ErrorCode.JoinInvalidSide => StatusCodes.Status400BadRequest,
        ErrorCode.MoveIllegalCell => StatusCodes.Status400BadRequest,
        ErrorCode.MoveCellOccupied => StatusCodes.Status400BadRequest,
        ErrorCode.MoveNotYourTurn => StatusCodes.Status400BadRequest,

        // 401 — cookie missing, tampered, expired, or playerId mismatch
        ErrorCode.SessionUnauthorized => StatusCodes.Status401Unauthorized,

        // 404 — opaque code didn't resolve to a live room
        ErrorCode.RoomNotFound => StatusCodes.Status404NotFound,

        // 409 — room state precludes the request (seat taken, not joinable,
        // match not in progress)
        ErrorCode.RoomAlreadyJoined => StatusCodes.Status409Conflict,
        ErrorCode.RoomNotJoinable => StatusCodes.Status409Conflict,
        ErrorCode.MoveMatchNotInProgress => StatusCodes.Status409Conflict,

        // 429 — lock contention; retry shortly
        ErrorCode.RoomBusy => StatusCodes.Status429TooManyRequests,

        _ => StatusCodes.Status400BadRequest,
    };
}
