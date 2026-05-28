using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.StackExchangeRedis;
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

        // Persist Data Protection keys to Redis. Without this, the keys live
        // on the container filesystem and disappear on every restart, which
        // means every redeploy invalidates every outstanding session cookie
        // and players in flight see "errors.session.unauthorized" on their
        // next move. Persisting to Redis also means the key ring is shared
        // across API instances if we ever horizontally scale beyond one B1.
        //
        // SetApplicationName matters for two reasons: (1) it namespaces the
        // keys in Redis so a second app sharing the same Redis won't collide,
        // and (2) it acts as the cookie purpose discriminator, so a deploy
        // that renames it deliberately invalidates outstanding cookies.
        // Don't change this string casually — see docs/deployment.md §6.6.
        //
        // The IConnectionMultiplexer is registered in AddInfrastructure as a
        // singleton; configure KeyManagementOptions via the SP-aware overload
        // so we share that one connection instead of opening a second one.
        // The PersistKeysToStackExchangeRedis extension takes a parameterless
        // Func<IDatabase> and would need either a captured singleton or a
        // BuildServiceProvider() anti-pattern; the options-Configure route is
        // the documented DI-friendly path.
        services.AddDataProtection()
            .SetApplicationName("playme-api");
        services.AddOptions<KeyManagementOptions>()
            .Configure<IConnectionMultiplexer>((options, redis) =>
            {
                options.XmlRepository = new RedisXmlRepository(
                    () => redis.GetDatabase(),
                    "playme:dp-keys");
            });

        services.AddSingleton<ISessionTokenService>(sp =>
        {
            var dp = sp.GetRequiredService<IDataProtectionProvider>();
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
        // payloads (CLAUDE.md §2.4 / §2.6 single wire format). Handler-
        // internal validation (DisplayName.TryCreate, GameId.TryCreate,
        // mode-vs-side rules, GameOptions size cap) returns typed
        // PlatformErrors keys that are already i18n keys — no separate
        // validation framework is wired into the controller pipeline.
        services.AddControllers()
            .AddJsonOptions(o => PlayMeJsonOptions.ApplyTo(o.JsonSerializerOptions));

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
