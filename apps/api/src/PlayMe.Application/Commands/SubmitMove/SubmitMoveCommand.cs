using PlayMe.Application.Dtos;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.SubmitMove;

/// <summary>
/// Hub <c>SubmitMove</c> dispatch (CLAUDE.md §2.4). Identity comes from the
/// signed session cookie (§5.4); the move payload's per-game shape is parsed
/// by the registered <c>IGameMoveParser</c>.
/// </summary>
public sealed record SubmitMoveCommand(
    string RoomCode,
    string CallerPlayerId,
    Role CallerRole,
    MoveDto Move);
