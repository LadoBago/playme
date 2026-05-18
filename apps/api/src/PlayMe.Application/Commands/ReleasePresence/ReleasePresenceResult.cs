using PlayMe.Application.Dtos;

namespace PlayMe.Application.Commands.ReleasePresence;

/// <summary>
/// Outcome the hub should broadcast after a presence-release call.
/// Discriminated rather than two bools so it's a compile error to forget
/// a case as new release effects land.
/// </summary>
public enum PresenceReleaseEffect
{
    /// <summary>No broadcast — release was a no-op (stale session, already
    /// disconnected, or room state that doesn't notify peers).</summary>
    None,
    /// <summary>Emit <c>OpponentDisconnected</c> — caller dropped while the
    /// match was <c>InProgress</c>; reconnect grace may be scheduled.</summary>
    OpponentDisconnected,
    /// <summary>Emit <c>OpponentExited</c> — caller dropped from
    /// <c>Ended</c> or <c>AwaitingRematch</c>; room moved to <c>Closed</c>
    /// (state.md §2.4 invariant: tab-close from those states is identical
    /// to an explicit <c>ExitRoom</c>).</summary>
    OpponentExited,
}

public sealed record ReleasePresenceResult(
    RoomDto Room,
    PresenceReleaseEffect Effect);
