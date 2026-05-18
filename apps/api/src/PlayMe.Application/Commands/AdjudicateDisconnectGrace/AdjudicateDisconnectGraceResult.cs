using PlayMe.Application.Dtos;

namespace PlayMe.Application.Commands.AdjudicateDisconnectGrace;

/// <summary>
/// Outcome of one grace-sweeper adjudication call.
/// <see cref="MatchEnded"/> is true when the abandon was applied — the
/// hub broadcasts <c>MatchEnded</c> in that case using the included
/// <see cref="Room"/>. False on any short-circuit (race lost to a
/// reconnect, turn flip, chess-clock timeout, or room cleanup); the
/// <see cref="Room"/> is null in those cases.
/// </summary>
public sealed record AdjudicateDisconnectGraceResult(RoomDto? Room, bool MatchEnded);
