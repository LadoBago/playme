namespace PlayMe.Application.Abstractions;

/// <summary>
/// Description of a per-session rate-limit policy: a stable name that
/// becomes part of the Redis key (so each policy gets an isolated
/// counter), a per-window limit, and the window length itself.
/// Concrete policies live in <c>PlayMe.Application.RateLimiting</c>.
/// </summary>
public sealed record RateLimitPolicy(string Name, int Limit, TimeSpan Window);
