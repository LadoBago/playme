namespace PlayMe.Application.Abstractions;

/// <summary>
/// Port for the Application-layer rate limiter (docs/security.md §5).
/// Enforces per-session quotas that must survive a SignalR reconnect —
/// the implementation in Infrastructure is a Redis sliding window keyed
/// by the policy name and a caller-supplied subject (typically the
/// session's <c>playerId</c>).
///
/// The pre-session (per-IP) and per-connection (burst) scopes are
/// enforced elsewhere: ASP.NET Core's <c>RateLimiter</c> middleware on
/// HTTP controllers and a SignalR <c>IHubFilter</c> on the WebSocket.
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// True if the action is allowed; false if the policy was exceeded
    /// for the given subject within its current window.
    /// </summary>
    Task<bool> TryAcquireAsync(
        RateLimitPolicy policy,
        string subjectKey,
        CancellationToken ct);
}
