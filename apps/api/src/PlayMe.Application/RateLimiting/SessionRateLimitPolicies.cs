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

    /// <summary>
    /// Resign is a one-shot action — the limit only exists to absorb
    /// double-click bursts on the confirm button. The handler is
    /// idempotent against an already-ended match, so a higher limit
    /// would still be safe; this just keeps the floor tight.
    /// </summary>
    public static readonly RateLimitPolicy Resign =
        new("resign", Limit: 3, Window: TimeSpan.FromSeconds(10));

    /// <summary>
    /// ExitRoom is one-shot ("Back to lobby" click). Idempotent against an
    /// already-Closed room, but the limit keeps a flood off the lock path.
    /// </summary>
    public static readonly RateLimitPolicy ExitRoom =
        new("exit", Limit: 3, Window: TimeSpan.FromSeconds(10));

    /// <summary>Rematch handshake calls — one click apiece, double-click
    /// absorbing limit. Shared policy across Offer/Accept/Reject keeps the
    /// floor tight without partitioning quota per sub-action.</summary>
    public static readonly RateLimitPolicy Rematch =
        new("rematch", Limit: 5, Window: TimeSpan.FromSeconds(10));

    /// <summary>
    /// SubmitSetup is one-shot per side per match (Sprint 10 seam C) —
    /// rerolls are client-local and never reach the server. The limit
    /// absorbs double-clicks on the commit button and keeps a flood off
    /// the room-lock path.
    /// </summary>
    public static readonly RateLimitPolicy SubmitSetup =
        new("setup", Limit: 5, Window: TimeSpan.FromSeconds(10));
}
