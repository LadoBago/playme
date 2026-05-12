using PlayMe.Domain.Platform;

namespace PlayMe.Application.Commands.AdjudicateDisconnectGrace;

/// <summary>
/// Sweeper-side dispatch when a <c>playme:grace</c> entry expires
/// (state.md §2.3, platform-and-games.md §1 #7). The sweeper has already
/// acquired the per-room distributed lock; this handler decides whether
/// the disconnected player is still gone after the 30 s window.
///
/// Sprint 2 only logs — Sprint 5 adds <c>OpponentAbandoned</c> + unlocks
/// <c>ClaimVictory</c>. Keeping the wiring in place now means Sprint 5
/// is a pure addition (no new sweeper plumbing).
/// </summary>
public sealed record AdjudicateDisconnectGraceCommand(string RoomCode, Role Role);
