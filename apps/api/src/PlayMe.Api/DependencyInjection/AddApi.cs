using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using PlayMe.Api.Hubs;
using PlayMe.Api.RateLimiting;
using PlayMe.Api.Security;
using PlayMe.Application.Abstractions;
using PlayMe.Infrastructure.Json;
using StackExchange.Redis;

namespace PlayMe.Api.DependencyInjection;

public static class ApiServiceCollectionExtensions
{
    /// <summary>
    /// Wires Api-layer services: controllers, SignalR + Redis backplane,
    /// CORS allowlist, data protection, session cookies, MVC + SignalR
    /// JSON serialization, and OpenTelemetry plumbing.
    /// </summary>
    public static IServiceCollection AddApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind cookie options + register session services (CLAUDE.md §5.4).
        services.AddOptions<SessionCookieOptions>()
            .Bind(configuration.GetSection(SessionCookieOptions.SectionName))
            .ValidateDataAnnotations();
        services.AddDataProtection();
        services.AddSingleton<ISessionTokenService>(sp =>
        {
            var dp = sp.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>();
            var clock = sp.GetRequiredService<IClock>();
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<SessionCookieOptions>>();
            return new SessionTokenService(dp, clock, opts.CurrentValue.MaxAge);
        });
        services.AddSingleton<SessionCookieWriter>();
        services.AddSingleton<SessionCookieReader>();

        // Sweeper-side broadcast hook (PR #2). Sprint 2 sweepers in
        // Infrastructure depend on IRoomNotifier so they can publish
        // MatchEnded without referencing SignalR directly.
        services.AddSingleton<IRoomNotifier, RoomNotifier>();

        // Controllers + JSON options aligned with PlayMeJsonOptions so
        // HTTP responses use the same shape as the Redis blob and SignalR
        // payloads (CLAUDE.md §2.4 / §2.6 single wire format).
        services.AddControllers()
            .AddJsonOptions(o => PlayMeJsonOptions.ApplyTo(o.JsonSerializerOptions));

        // Note: FluentValidation auto-validation is intentionally NOT wired.
        // Handler-internal validation (DisplayName.Create, GameId ctor,
        // mode-vs-side rules) returns typed PlatformErrors keys that are
        // already i18n keys — auto-validation's ValidationProblemDetails
        // would require an extra mapping layer for no real gain.

        services.AddEndpointsApiExplorer();
        services.AddOpenApi();

        // CORS — CLAUDE.md §5.7: explicit allowlist, never "*".
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:3000" };
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy => policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());
        });

        // Per-IP HTTP rate-limit policies (docs/security.md §5). Per-session
        // quotas (move flood, rematch spam) belong behind an Application-
        // layer IRateLimiter port — not wired here.
        services.AddPlayMeRateLimiting();

        // SignalR with Redis backplane (CLAUDE.md §2.1). Same JSON shape
        // as MVC via AddJsonProtocol. BurstHubFilter enforces the
        // per-connection burst ceiling (docs/security.md §5).
        services.AddSingleton<BurstHubFilter>();
        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? "localhost:6379";
        services.AddSignalR(o => o.AddFilter<BurstHubFilter>())
            .AddJsonProtocol(o => PlayMeJsonOptions.ApplyTo(o.PayloadSerializerOptions))
            .AddStackExchangeRedis(redisConnectionString, options =>
            {
                options.Configuration.ChannelPrefix =
                    RedisChannel.Literal("playme:signalr");
            });

        // OpenTelemetry — CLAUDE.md §4.4: v1 exports to stdout only.
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddConsoleExporter());

        return services;
    }
}
