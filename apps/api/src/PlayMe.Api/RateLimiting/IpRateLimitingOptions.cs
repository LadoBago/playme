namespace PlayMe.Api.RateLimiting;

/// <summary>
/// Permit limits for the per-IP HTTP policies in
/// <see cref="RateLimitingServiceCollectionExtensions"/>. Defaults match
/// the docs/security.md §5 starting points; the section exists so a
/// deliberate load-test window (docs/loadtest.md §6) can widen the limits
/// via App Service env vars (e.g. <c>RateLimiting__Ip__RoomsJoinPermitLimit</c>)
/// without a redeploy — every bot in a single-machine test run shares one
/// source IP, so the production defaults throttle the harness long before
/// they throttle the platform. Windows stay fixed at one minute; only the
/// permit counts are tunable.
/// </summary>
public sealed class IpRateLimitingOptions
{
    public const string SectionName = "RateLimiting:Ip";

    /// <summary>Permits per minute for <c>POST /api/rooms</c>.</summary>
    public int RoomsCreatePermitLimit { get; set; } = 10;

    /// <summary>Permits per minute for <c>POST /api/rooms/{code}/join</c>.</summary>
    public int RoomsJoinPermitLimit { get; set; } = 5;

    /// <summary>Permits per minute for <c>GET /api/rooms/{code}</c>.</summary>
    public int RoomsGetPermitLimit { get; set; } = 60;
}
