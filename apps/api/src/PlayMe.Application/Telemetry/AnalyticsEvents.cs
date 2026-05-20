using PlayMe.Application.Mapping;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Telemetry;

/// <summary>
/// Server-side event catalog for <see cref="Abstractions.IAnalyticsClient"/>.
/// Names + property shapes mirror the web's <c>AnalyticsEvent</c> union in
/// <c>apps/web/lib/analytics.ts</c> so a given event reads the same in
/// PostHog regardless of which end emitted it (the adapter tags
/// <c>source: server</c> vs the web's <c>source: web</c>).
///
/// Web emits user-action events; the server emits authoritative
/// outcomes — currently just <see cref="MatchEnded"/>; <c>room_expired</c>
/// joins when the room-expiry sweeper lands.
/// </summary>
public static class AnalyticsEvents
{
    public const string MatchEnded = "match_ended";
    public const string RoomExpired = "room_expired";

    /// <summary>
    /// Build the <c>match_ended</c> properties dictionary. <c>reason</c> is
    /// the same five-value discriminant the web sees on the wire — sourced
    /// through <see cref="RoomMapper.ToOutcomeDto"/> so there's one switch
    /// statement governing both the SignalR DTO and the analytics event.
    /// </summary>
    public static IReadOnlyDictionary<string, object?> MatchEndedProperties(
        string gameId, Outcome outcome) =>
        new Dictionary<string, object?>
        {
            ["gameId"] = gameId,
            ["reason"] = RoomMapper.ToOutcomeDto(outcome).Kind,
        };

    /// <summary>
    /// Build the <c>room_expired</c> properties dictionary. Fired only
    /// for the <c>WaitingForOpponent</c> → <c>Expired</c> case — the
    /// product-meaningful "nobody joined" funnel signal. Cleanup-TTL
    /// expiries of terminal-state rooms are not tracked (just GC).
    /// </summary>
    public static IReadOnlyDictionary<string, object?> RoomExpiredProperties(
        string gameId) =>
        new Dictionary<string, object?>
        {
            ["gameId"] = gameId,
        };
}
