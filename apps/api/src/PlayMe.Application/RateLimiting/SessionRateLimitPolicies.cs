using PlayMe.Application.Abstractions;

namespace PlayMe.Application.RateLimiting;

/// <summary>
/// Per-session rate-limit policies (docs/security.md §5). These survive
/// SignalR reconnects (the Redis sliding window is keyed by the
/// session's <c>playerId</c>) and complement the pre-session per-IP
/// policies enforced at the controller layer.
/// </summary>
public static class SessionRateLimitPolicies
{
    /// <summary>60 SubmitMove invocations per minute, sustained.</summary>
    public static readonly RateLimitPolicy SubmitMove =
        new("move", Limit: 60, Window: TimeSpan.FromMinutes(1));
}
