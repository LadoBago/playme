namespace PlayMe.Application.Abstractions;

/// <summary>
/// Server-side product analytics port. The web emits user-action events
/// (room_created, room_joined, match_started, move_made, rematch_*); the
/// server emits authoritative outcomes (match_ended, room_expired) so the
/// catalog stays accurate when a client disconnects before it can report
/// (docs/observability-and-i18n.md §1.2).
///
/// Implementations MUST treat tracking as fire-and-forget: a failure to
/// reach the analytics backend never propagates to gameplay paths. The
/// adapter logs and swallows; <see cref="TrackAsync"/> never throws.
///
/// The <paramref name="distinctId"/> is opaque to the platform. For
/// anonymous events from this codebase it's typically the room code —
/// person-profile creation is suppressed on the adapter side so this
/// doesn't inflate PostHog's person counts.
/// </summary>
public interface IAnalyticsClient
{
    Task TrackAsync(
        string eventName,
        string distinctId,
        IReadOnlyDictionary<string, object?> properties,
        CancellationToken ct = default);
}
