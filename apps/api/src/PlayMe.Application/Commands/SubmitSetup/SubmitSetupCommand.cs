using PlayMe.Application.Dtos;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.SubmitSetup;

/// <summary>
/// One side's single, final setup commit (Sprint 10 seam C). The payload
/// is the module's own setup shape (e.g. sea battle's fleet), parsed by
/// the game's <see cref="Abstractions.IGameMoveParser"/> and never
/// inspected by the platform.
/// </summary>
public sealed record SubmitSetupCommand(
    string RoomCode,
    string CallerPlayerId,
    Role CallerRole,
    MoveDto Setup);
