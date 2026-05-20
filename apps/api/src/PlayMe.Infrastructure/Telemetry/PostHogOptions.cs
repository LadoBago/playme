namespace PlayMe.Infrastructure.Telemetry;

/// <summary>
/// Configuration for <see cref="PostHogAnalyticsClient"/>. Bound from the
/// <c>PostHog</c> section of <c>IConfiguration</c> via <c>IOptions&lt;T&gt;</c>
/// per CLAUDE.md §6 — never read <c>IConfiguration</c> from business code.
///
/// The API key is a project-scoped capture-only key (PostHog calls it the
/// "Project API key"). It identifies which project events land in; it is
/// not a secret in the same sense as a server admin key, but we still load
/// it from env / user-secrets rather than committing it.
/// </summary>
public sealed class PostHogOptions
{
    /// <summary>
    /// PostHog project API key. Empty/null disables server-side analytics
    /// (the DI wiring falls back to <c>NoOpAnalyticsClient</c>) so local
    /// dev and tests don't emit events.
    /// </summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// Capture API host. Defaults to the EU instance to match the web SDK
    /// (apps/web/lib/analytics.ts). Override for self-hosted PostHog or to
    /// route through a regional endpoint.
    /// </summary>
    public string Host { get; init; } = "https://eu.i.posthog.com";
}
