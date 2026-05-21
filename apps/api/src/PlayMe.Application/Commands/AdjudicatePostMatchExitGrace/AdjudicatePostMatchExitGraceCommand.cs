using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.AdjudicatePostMatchExitGrace;

/// <summary>
/// Sweeper-side dispatch when a <c>playme:postmatch_exit</c> entry
/// expires (state.md §2.4). The sweeper has already acquired the
/// per-room lock; this handler decides whether the post-match
/// disconnect is still in effect after the grace window — if so, the
/// room transitions to <see cref="RoomStatus.Closed"/> and the sweeper
/// broadcasts <c>OpponentExited</c> to the still-connected player.
/// </summary>
public sealed record AdjudicatePostMatchExitGraceCommand(string RoomCode, Role Role);
