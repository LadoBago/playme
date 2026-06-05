namespace PlayMe.Api.RateLimiting;

/// <summary>
/// Permit limits for the per-IP HTTP policies in
/// <see cref="RateLimitingServiceCollectionExtensions"/>, per docs/security.md §5.
/// The section is bindable so a deliberate load-test window (docs/loadtest.md §8.1)
/// can widen the limits via App Service env vars
/// (e.g. <c>RateLimiting__Ip__RoomsJoinPermitLimit</c>) without a redeploy.
/// Windows stay fixed at one minute; only the permit counts are tunable.
/// <para>
/// The defaults are sized for **shared-IP (corporate NAT / proxy) traffic**:
/// these endpoints gate match *setup rate*, not concurrent-match count (a
/// live match runs entirely over the per-session/per-connection SignalR path,
/// which is not keyed by IP). The counts let ~20 matches start from a single
/// source IP within one minute, with headroom. The abuse trade is accepted
/// deliberately: rooms are anonymous, ephemeral (a waiting room self-expires
/// in 30 min) and hold no PII, so a high creation ceiling costs only transient
/// Redis memory — see docs/security.md §5.
/// </para>
/// </summary>
public sealed class IpRateLimitingOptions
{
    public const string SectionName = "RateLimiting:Ip";

    /// <summary>Permits per minute for <c>POST /api/rooms</c>.</summary>
    public int RoomsCreatePermitLimit { get; set; } = 30;

    /// <summary>Permits per minute for <c>POST /api/rooms/{code}/join</c>.</summary>
    public int RoomsJoinPermitLimit { get; set; } = 30;

    /// <summary>Permits per minute for <c>GET /api/rooms/{code}</c>.</summary>
    public int RoomsGetPermitLimit { get; set; } = 120;
}
