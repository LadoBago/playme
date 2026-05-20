using PlayMe.Application.Abstractions;

namespace PlayMe.Application.Tests.Fakes;

/// <summary>
/// Test fake for <see cref="IAnalyticsClient"/>. Captures every
/// <see cref="TrackAsync"/> call so handler tests can assert on the
/// emitted event name + properties. Default behaviour is silent — the
/// handler tests that don't care about analytics just construct one
/// and ignore the recorded list.
/// </summary>
public sealed class RecordingAnalyticsClient : IAnalyticsClient
{
    public List<(string Event, string DistinctId, IReadOnlyDictionary<string, object?> Properties)>
        Events
    { get; } = new();

    public Task TrackAsync(
        string eventName,
        string distinctId,
        IReadOnlyDictionary<string, object?> properties,
        CancellationToken ct = default)
    {
        Events.Add((eventName, distinctId, properties));
        return Task.CompletedTask;
    }
}
