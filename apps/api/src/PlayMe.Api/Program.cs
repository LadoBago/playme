using System.Globalization;
using PlayMe.Api.DependencyInjection;
using PlayMe.Api.Middleware;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Sentry (CLAUDE.md §4.1, §5.8). DSN comes from configuration:
// `Sentry:Dsn` in appsettings, env var `SENTRY__DSN`, or user-secrets
// for local dev. No DSN -> SDK initializes but stays disabled.
// Sentry (CLAUDE.md §4.1, §5.8). DSN comes from configuration:
// `Sentry:Dsn` in appsettings, env var `SENTRY__DSN`, or user-secrets
// for local dev. No DSN -> SDK initializes but stays disabled.
var sentryDsn = builder.Configuration["Sentry:Dsn"];
builder.WebHost.UseSentry(options =>
{
    options.Dsn = sentryDsn;
    options.SendDefaultPii = false;
    options.TracesSampleRate = 0;
    options.Release = typeof(Program).Assembly.GetName().Version?.ToString();
    options.Environment = builder.Environment.EnvironmentName;
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

app.UseSerilogRequestLogging();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseCors();
app.UseRateLimiter();
app.MapControllers();
app.MapHub<PlayMe.Api.Hubs.RoomHub>("/hubs/room");

await app.RunAsync();

/// <summary>Exposed for WebApplicationFactory in integration tests.</summary>
public partial class Program;
