using PlayMe.Application.Abstractions;

namespace PlayMe.Application.Tests.Fakes;

/// <summary>
/// Test fake for <see cref="IRateLimiter"/>. Defaults to allowing every
/// call (so existing tests don't have to thread quota behaviour through);
/// flip <see cref="AllowNext"/> to false to assert the rate-exceeded path.
/// </summary>
public sealed class RecordingRateLimiter : IRateLimiter
{
    public bool AllowNext { get; set; } = true;

    public List<(RateLimitPolicy Policy, string Subject)> Calls { get; } = new();

    public Task<bool> TryAcquireAsync(
        RateLimitPolicy policy,
        string subjectKey,
        CancellationToken ct)
    {
        Calls.Add((policy, subjectKey));
        return Task.FromResult(AllowNext);
    }
}
