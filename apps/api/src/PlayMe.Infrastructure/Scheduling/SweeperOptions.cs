namespace PlayMe.Infrastructure.Scheduling;

/// <summary>
/// Tunables shared by the timeout and disconnect-grace sweepers.
/// Defaults match state.md §2.2: ~250 ms scan interval, ~100 ms lock
/// acquire wait, batch of up to 32 entries per tick.
/// </summary>
public sealed record SweeperOptions
{
    /// <summary>Wall-clock interval between scans.</summary>
    public TimeSpan ScanInterval { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>Max entries drained per <c>ZRANGEBYSCORE</c> call.</summary>
    public int BatchSize { get; init; } = 32;

    /// <summary>
    /// Cap on per-entry lock acquisition. If contended, we skip — the
    /// next sweep retries. Short on purpose: timeout adjudication is
    /// idempotent and re-attempting is cheaper than blocking the loop.
    /// </summary>
    public TimeSpan LockAcquireBudget { get; init; } = TimeSpan.FromMilliseconds(100);
}
