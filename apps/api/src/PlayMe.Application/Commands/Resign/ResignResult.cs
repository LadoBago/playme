using PlayMe.Application.Dtos;

namespace PlayMe.Application.Commands.Resign;

/// <summary>
/// Result of an accepted <c>Resign</c>. <see cref="TimedOut"/> distinguishes
/// the case where the caller's clock had already run out at the moment of the
/// resign submission: instead of <see cref="Domain.Platform.Outcome"/> <c>Resign</c>,
/// the match is closed with <c>Timeout</c> — the same stale-clock conversion
/// the move pipeline performs. The Hub broadcasts <c>MatchEnded</c> regardless;
/// callers may inspect this flag for analytics.
/// </summary>
public sealed record ResignResult(RoomDto Room, bool TimedOut);
