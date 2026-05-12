using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
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

        // Controllers + JSON options aligned with PlayMeJsonOptions so
        // HTTP responses use the same shape as the Redis blob and SignalR
        // payloads (CLAUDE.md §2.4 / §2.6 single wire format).
        services.AddControllers()
            .AddJsonOptions(o => PlayMeJsonOptions.ApplyTo(o.JsonSerializerOptions));

        // Note: FluentValidation auto-validation is intentionally NOT wired.
        // Handler-internal validation (DisplayName.Create, GameId ctor,
        // mode-vs-side rules) returns typed ErrorCode values that map 1:1
        // to §3 i18n keys — auto-validation's ValidationProblemDetails
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

        // SignalR with Redis backplane (CLAUDE.md §2.1). Same JSON shape
        // as MVC via AddJsonProtocol.
        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? "localhost:6379";
        services.AddSignalR()
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
