namespace PlayMe.Api.Controllers;

/// <summary>Response body for <see cref="HealthController.Get"/>.</summary>
public sealed record HealthResponse(
    string Status,
    string Service,
    string Version,
    DateTimeOffset Timestamp);
