namespace PlayMe.Api.RateLimiting;

/// <summary>
/// Centralised policy names so controller <c>[EnableRateLimiting]</c>
/// attributes and the DI wiring don't drift from magic strings.
/// Policy limits are defined in <see cref="RateLimitingServiceCollectionExtensions"/>
/// per <c>docs/security.md §5</c>.
/// </summary>
public static class RateLimitingPolicies
{
    /// <summary><c>POST /api/rooms</c> — per IP, 10/min.</summary>
    public const string RoomsCreate = "rooms-create";

    /// <summary><c>POST /api/rooms/{code}/join</c> — per IP, 5/min.</summary>
    public const string RoomsJoin = "rooms-join";
}
