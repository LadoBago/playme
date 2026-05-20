using PlayMe.Application.Abstractions;

namespace PlayMe.Infrastructure.Telemetry;

/// <summary>
/// Fallback used when <see cref="PostHogOptions.ApiKey"/> is empty —
/// local dev, integration tests, and the first boot in a fresh
/// environment all get this. Drops every event silently so calling
/// code is unconditional ("always Track"; the wiring decides whether
/// the event actually leaves the process).
/// </summary>
public sealed class NoOpAnalyticsClient : IAnalyticsClient
{
    public Task TrackAsync(
        string eventName,
        string distinctId,
        IReadOnlyDictionary<string, object?> properties,
        CancellationToken ct = default) => Task.CompletedTask;
}
