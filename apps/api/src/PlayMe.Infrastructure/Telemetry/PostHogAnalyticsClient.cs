using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlayMe.Application.Abstractions;

namespace PlayMe.Infrastructure.Telemetry;

/// <summary>
/// PostHog capture-only adapter. Posts events to PostHog's standard
/// capture endpoint (<c>POST /i/v0/e/</c>) using a typed
/// <see cref="HttpClient"/>. We don't take the official PostHog .NET
/// SDK because it pulls nine transitive packages for the ~5% surface
/// we use (no feature flags, no identify, no LLM observability) — see
/// the PR description for the comparison. If we ever need the wider
/// surface, the swap is one file: <c>AddInfrastructure</c> registers
/// the official adapter against the same <see cref="IAnalyticsClient"/>
/// port and every call site is untouched.
///
/// Wire shape (PostHog capture API):
/// <code>
/// {
///   "api_key": "phc_...",
///   "event":   "match_ended",
///   "distinct_id": "ABCD1234",
///   "properties": {
///     "gameId": "tictactoe-3x3",
///     "reason": "win",
///     "source": "server",
///     "$process_person_profile": false
///   },
///   "timestamp": "2026-05-20T12:34:56.789Z"
/// }
/// </code>
///
/// <c>source: server</c> distinguishes these from web events (the web
/// adapter tags them <c>source: web</c>). <c>$process_person_profile:
/// false</c> stops PostHog from materialising a "person" for every room
/// code — without it our anonymous distinct_ids would inflate the
/// project's person count and quota.
///
/// Tracking is fire-and-forget. The capture endpoint is fast (~tens of
/// ms typical), but a slow or failed call must never delay or break a
/// match-end broadcast. Exceptions are logged at Warning and swallowed.
/// </summary>
public sealed partial class PostHogAnalyticsClient : IAnalyticsClient
{
    private readonly HttpClient _http;
    private readonly PostHogOptions _options;
    private readonly ILogger<PostHogAnalyticsClient> _logger;

    public PostHogAnalyticsClient(
        HttpClient http,
        IOptions<PostHogOptions> options,
        ILogger<PostHogAnalyticsClient> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task TrackAsync(
        string eventName,
        string distinctId,
        IReadOnlyDictionary<string, object?> properties,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentException.ThrowIfNullOrWhiteSpace(distinctId);
        ArgumentNullException.ThrowIfNull(properties);

        // Compose the wire payload. The adapter — not the caller — owns
        // the cross-cutting properties (`source`, `$process_person_profile`)
        // so call sites can't forget them or drift from the web's tagging.
        var props = new Dictionary<string, object?>(properties)
        {
            ["source"] = "server",
            ["$process_person_profile"] = false,
        };

        var payload = new CaptureRequest(
            ApiKey: _options.ApiKey,
            Event: eventName,
            DistinctId: distinctId,
            Properties: props,
            Timestamp: DateTimeOffset.UtcNow.ToString("O"));

        try
        {
            using var response = await _http.PostAsJsonAsync(
                "/i/v0/e/", payload, ct);
            // PostHog returns 200 on success. Any non-2xx is a soft
            // failure — log the status so we can investigate, but never
            // surface to the caller.
            if (!response.IsSuccessStatusCode)
            {
                LogCaptureNonSuccess(_logger, (int)response.StatusCode, eventName);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The caller cancelled (e.g. request shutdown). Don't log —
            // this isn't a failure, just an early exit.
            throw;
        }
        catch (Exception ex)
        {
            // Connection timeouts, DNS failures, malformed responses —
            // all soft-fail. Analytics must never break gameplay.
            LogCaptureThrew(_logger, ex, eventName);
        }
    }

    // Wire-format record. `JsonPropertyName` ensures the camel_case /
    // snake_case keys PostHog expects, regardless of the project's
    // System.Text.Json defaults.
    private sealed record CaptureRequest(
        [property: JsonPropertyName("api_key")] string ApiKey,
        [property: JsonPropertyName("event")] string Event,
        [property: JsonPropertyName("distinct_id")] string DistinctId,
        [property: JsonPropertyName("properties")] IReadOnlyDictionary<string, object?> Properties,
        [property: JsonPropertyName("timestamp")] string Timestamp);

    [LoggerMessage(
        EventId = 2200,
        Level = LogLevel.Warning,
        Message = "PostHog capture returned non-success status {Status} for event {Event}")]
    private static partial void LogCaptureNonSuccess(ILogger logger, int status, string @event);

    [LoggerMessage(
        EventId = 2201,
        Level = LogLevel.Warning,
        Message = "PostHog capture threw for event {Event}; dropping event")]
    private static partial void LogCaptureThrew(ILogger logger, Exception ex, string @event);
}
