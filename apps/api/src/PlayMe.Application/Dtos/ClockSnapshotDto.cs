namespace PlayMe.Application.Dtos;

/// <summary>
/// Wire view of <see cref="Domain.Platform.MatchClock"/> at a given server
/// moment. Times are sent as integer milliseconds — JSON-friendly and
/// matches the client extrapolation arithmetic in state.md §2.2.
/// <see cref="ServerNowAt"/> lets the client compute its local-vs-server
/// delta and extrapolate the active player's clock between snapshots
/// without trusting <c>Date.now()</c> against <see cref="LastTickAt"/>
/// directly.
/// </summary>
/// <param name="HostMs">Host's effective remaining ms <em>at
/// <see cref="ServerNowAt"/></em> (extrapolation already applied
/// server-side for the active player). Clients add only their own
/// local-vs-serverNowAt delta on top.</param>
/// <param name="ChallengerMs">Challenger's effective remaining ms at
/// <see cref="ServerNowAt"/>.</param>
/// <param name="ActivePlayer">"host" or "challenger" — the slot whose
/// clock is currently ticking.</param>
/// <param name="LastTickAt">Server UTC moment of the last clock mutation
/// (match start, last accepted move, or timeout). Diagnostic; clients
/// don't need it for extrapolation.</param>
/// <param name="ServerNowAt">Server UTC moment when this snapshot was
/// serialized. Always ≥ <see cref="LastTickAt"/>.</param>
public sealed record ClockSnapshotDto(
    long HostMs,
    long ChallengerMs,
    string ActivePlayer,
    DateTimeOffset LastTickAt,
    DateTimeOffset ServerNowAt);
