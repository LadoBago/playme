using PlayMe.Application.Abstractions;
using PlayMe.Application.Dtos;
using PlayMe.Application.Errors;
using PlayMe.Application.Mapping;
using PlayMe.Domain.Games.TicTacToe3x3;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.SubmitMove;

/// <summary>
/// The authoritative move pipeline (CLAUDE.md §2.1: "The server is the
/// single source of truth"). Inside the room's distributed lock: authorize
/// the caller, verify the match is in progress and the caller is the active
/// player, parse and apply the move via the game module, commit if accepted,
/// and persist.
/// </summary>
public sealed class SubmitMoveHandler
{
    private readonly IRoomRepository _rooms;
    private readonly IGameModuleRegistry _games;

    public SubmitMoveHandler(IRoomRepository rooms, IGameModuleRegistry games)
    {
        _rooms = rooms;
        _games = games;
    }

    public async Task<AppResult<SubmitMoveResult>> HandleAsync(
        SubmitMoveCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        RoomCode code;
        try { code = new RoomCode(cmd.RoomCode); }
        catch (ArgumentException)
        {
            return AppResult<SubmitMoveResult>.Fail(ErrorCode.RoomNotFound);
        }

        try
        {
            return await _rooms.WithLockAsync(code, async () =>
            {
                var room = await _rooms.LoadAsync(code, ct);
                if (room is null)
                {
                    return AppResult<SubmitMoveResult>.Fail(ErrorCode.RoomNotFound);
                }

                var stored = room.PlayerFor(cmd.CallerRole);
                if (stored is null || stored.Id.Value != cmd.CallerPlayerId)
                {
                    return AppResult<SubmitMoveResult>.Fail(ErrorCode.SessionUnauthorized);
                }

                if (room.Status != RoomStatus.InProgress || room.CurrentMatch is null)
                {
                    return AppResult<SubmitMoveResult>.Fail(ErrorCode.MoveMatchNotInProgress);
                }

                var match = room.CurrentMatch;
                if (match.IsEnded)
                {
                    return AppResult<SubmitMoveResult>.Fail(ErrorCode.MoveMatchNotInProgress);
                }

                var callerSide = stored.Side;
                if (callerSide is null || callerSide != match.SideToMove)
                {
                    return AppResult<SubmitMoveResult>.Fail(ErrorCode.MoveNotYourTurn);
                }

                var parser = _games.GetMoveParser(room.GameId);
                var parseResult = parser.Parse(cmd.Move);
                if (!parseResult.Succeeded)
                {
                    return parseResult.ToFailure<SubmitMoveResult>();
                }

                var module = _games.GetModule(room.GameId);
                var moveResult = module.ApplyMove(match.State, callerSide, parseResult.Value!);
                if (!moveResult.Accepted)
                {
                    var errorCode = moveResult.RejectReason switch
                    {
                        MoveRejectReason.IllegalCell => ErrorCode.MoveIllegalCell,
                        MoveRejectReason.CellOccupied => ErrorCode.MoveCellOccupied,
                        // Sprint 1 has no Connect 4; FullColumn isn't reachable yet.
                        _ => ErrorCode.ValidationMove,
                    };
                    return AppResult<SubmitMoveResult>.Fail(errorCode);
                }

                var nextSide = module.OtherSide(callerSide);
                match.ApplyAcceptedMove(moveResult.NewState!, nextSide, moveResult.Ending);
                if (moveResult.Ending is not null)
                {
                    room.EndCurrentMatch();
                }
                await _rooms.SaveAsync(room, ct);

                return AppResult<SubmitMoveResult>.Ok(new SubmitMoveResult(
                    Room: RoomMapper.ToDto(room),
                    MatchEnded: moveResult.Ending is not null,
                    AcceptedCell: ExtractCell(parseResult.Value!),
                    ByMoveSide: callerSide));
            }, ct);
        }
        catch (LockTimeoutException)
        {
            return AppResult<SubmitMoveResult>.Fail(ErrorCode.RoomBusy);
        }
    }

    /// <summary>
    /// Pull the move's primary coordinate for the Hub's broadcast event.
    /// Sprint 1 only knows Tic-Tac-Toe; refactor when Connect 4 arrives.
    /// </summary>
    private static int ExtractCell(GameMove move) => move switch
    {
        TicTacToeMove t => t.Cell,
        _ => throw new InvalidOperationException(
            $"No cell extractor for move type {move.GetType().Name}."),
    };
}
