using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using PlayMe.Api.DependencyInjection;
using PlayMe.Api.Middleware;
using PlayMe.Application.Abstractions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Lets the Sentry BeforeSend hook below read the in-flight request's
// RequestAborted token (registration makes the framework populate the
// ambient HttpContext; the hook reads it via a plain accessor instance —
// HttpContextAccessor's backing store is a shared AsyncLocal).
builder.Services.AddHttpContextAccessor();
var httpContextAccessor = new HttpContextAccessor();

// Sentry (CLAUDE.md §4.1, §5.8). DSN comes from configuration:
// `Sentry:Dsn` in appsettings, env var `SENTRY__DSN`, or user-secrets
// for local dev. The SDK requires an empty string (not null) to stay
// disabled — coalesce so a missing config key doesn't crash startup.
var sentryDsn = builder.Configuration["Sentry:Dsn"] ?? string.Empty;
builder.WebHost.UseSentry(options =>
{
    options.Dsn = sentryDsn;
    options.SendDefaultPii = false;
    options.TracesSampleRate = 0.1;
    options.Release = typeof(Program).Assembly.GetName().Version?.ToString();
    options.Environment = builder.Environment.EnvironmentName;
    // HubException is SignalR's controlled, client-facing error vehicle.
    // Every throw in RoomHub carries an i18n key from PlatformErrors and is
    // part of the protocol, not a server bug — `RequireSession()` in
    // particular fires by design when the room page mounts the hub before
    // a session cookie exists for this room. SignalR's DefaultHubDispatcher
    // still logs the throw at Error level, which the Serilog sink below
    // would otherwise forward to Sentry as noise.
    options.AddExceptionFilterForType<HubException>();

    // Drop OperationCanceledException ONLY when the client aborted the
    // request — a player closing the tab, locking their phone, or dropping
    // WiFi mid-move cancels Context.ConnectionAborted / RequestAborted, and
    // every hub method threads that token into its handler, so the awaited
    // work throws OCE up through the pipeline. That is expected churn, not a
    // server fault. We do NOT blanket-filter the type: an OCE that fires
    // *without* the request having aborted (a genuine server-side
    // cancellation or bug) is not a client disconnect, so it still reaches
    // Sentry. This runs on the shared hub, so it covers both the
    // ASP.NET Core integration and the Serilog sink (InitializeSdk=false).
    // A null HttpContext (capture off the request context) falls through to
    // "send" on purpose — we only suppress what we can prove is benign.
    options.SetBeforeSend((@event, _) =>
        @event.Exception is OperationCanceledException
        && httpContextAccessor.HttpContext?.RequestAborted.IsCancellationRequested == true
            ? null
            : @event);
});

// Serilog (CLAUDE.md §4.3): structured logging + 7-day rolling file sink.
// The Sentry sink runs alongside Console + File. Because UseSerilog
// replaces the default Microsoft.Extensions.Logging pipeline, automatic
// exception capture via `Sentry.AspNetCore`'s logging hook no longer
// fires; routing Error/Fatal Serilog events to Sentry replaces it.
builder.Host.UseSerilog((ctx, services, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .WriteTo.File(
        path: "logs/playme-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        formatProvider: CultureInfo.InvariantCulture)
    .WriteTo.Sentry(s =>
    {
        s.Dsn = sentryDsn;
        s.InitializeSdk = false; // SDK already init'd by UseSentry
        s.MinimumEventLevel = Serilog.Events.LogEventLevel.Error;
        s.MinimumBreadcrumbLevel = Serilog.Events.LogEventLevel.Information;
    }));

builder.Services
    .AddDomain()
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddApi(builder.Configuration);

var app = builder.Build();

// Serilog request logging emits a single Information entry per request
// with the path templated in. The default `RequestPath` would include
// the raw room code on `/api/rooms/{code}/...` routes — room codes are
// 128-bit invite tokens (docs/security.md §8) and must not appear in
// stored logs. Override `RequestPath` with a redacted version so log
// lines on the same room still correlate via the hashed token.
app.UseSerilogRequestLogging(opts =>
    opts.EnrichDiagnosticContext = (diag, http) =>
    {
        // The Serilog request-logging middleware seeds `RequestPath` from
        // `IHttpRequestFeature.RawTarget` (path + query) before invoking
        // this enricher; we override that property with the same string,
        // minus the room code segment. `IDiagnosticContext.Set` is
        // add-or-replace by name, so this wins.
        var rawTarget = http.Features.Get<IHttpRequestFeature>()?.RawTarget
            ?? http.Request.Path.Value
            ?? string.Empty;
        var redactor = http.RequestServices.GetService<IRoomCodeRedactor>();
        diag.Set("RequestPath", RedactRoomCodeInPath(rawTarget, redactor), destructureObjects: false);
    });
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseCors();
app.UseRateLimiter();
app.MapControllers();
app.MapHub<PlayMe.Api.Hubs.RoomHub>("/hubs/room");

await app.RunAsync();

/// <summary>Exposed for WebApplicationFactory in integration tests.</summary>
public partial class Program
{
    /// <summary>
    /// Replaces the room code segment in <paramref name="path"/> with the
    /// redacted token so the Serilog request-logging entry can be safely
    /// stored. Matches both <c>/api/rooms/{code}</c> and
    /// <c>/api/rooms/{code}/...</c>. Falls back to a static <c>rc:redacted</c>
    /// marker when the redactor port isn't available (e.g. very early in
    /// the request pipeline) — that branch is defensive; in practice
    /// <see cref="IRoomCodeRedactor"/> is registered as a singleton.
    /// </summary>
    private static string RedactRoomCodeInPath(string path, IRoomCodeRedactor? redactor)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        return RoomCodePathRegex().Replace(path, match =>
        {
            var prefix = match.Groups["prefix"].Value;
            var code = match.Groups["code"].Value;
            var tail = match.Groups["tail"].Value;
            var redacted = redactor?.Redact(code) ?? "rc:redacted";
            return string.Concat(prefix, redacted, tail);
        });
    }

    [GeneratedRegex(
        @"^(?<prefix>/api/rooms/)(?<code>[^/?#]+)(?<tail>(?:[/?#].*)?)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RoomCodePathRegex();
}
