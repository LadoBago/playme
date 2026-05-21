using PlayMe.Application.Dtos;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.AdjudicatePostMatchExitGrace;

/// <summary>
/// Outcome of one post-match grace-sweeper adjudication call.
/// <see cref="Exited"/> is true when the grace was applied and the room
/// transitioned to <see cref="RoomStatus.Closed"/> — the sweeper
/// broadcasts <c>OpponentExited</c> in that case, addressed to the
/// still-connected player, with the <see cref="ExitedRole"/> as the
/// leaving party. False on any short-circuit (race lost to a reconnect,
/// room already closed, room reaped); <see cref="Room"/> is null and
/// <see cref="ExitedRole"/> is unspecified in those cases.
/// </summary>
public sealed record AdjudicatePostMatchExitGraceResult(
    RoomDto? Room,
    bool Exited,
    Role ExitedRole);
