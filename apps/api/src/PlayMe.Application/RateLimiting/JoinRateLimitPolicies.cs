using PlayMe.Application.Abstractions;

namespace PlayMe.Application.RateLimiting;

/// <summary>
/// Application-layer rate-limit policies that key on something *other*
/// than the caller's session — `JoinRoom` is pre-session, so the natural
/// partition is the room code itself. Per <c>docs/security.md §5</c>:
/// the per-IP dimension is enforced at the controller via ASP.NET's
/// middleware (<c>RoomsJoin</c> policy); the per-code dimension runs
/// here so a single shared invite link can't be rejoined from many IPs
/// at machine speed.
/// </summary>
public static class JoinRateLimitPolicies
{
    /// <summary>10 join attempts per hour per room code.</summary>
    public static readonly RateLimitPolicy ByCode =
        new("join-code", Limit: 10, Window: TimeSpan.FromHours(1));
}
