using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;
using StackExchange.Redis;

namespace PlayMe.Api.DependencyInjection;

public static class ApiServiceCollectionExtensions
{
    /// <summary>
    /// Wires Api-layer services: controllers, SignalR + Redis backplane,
    /// CORS allowlist, and OpenTelemetry plumbing.
    /// </summary>
    public static IServiceCollection AddApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddControllers();
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

        // SignalR with Redis backplane (CLAUDE.md §2.1).
        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? "localhost:6379";
        services.AddSignalR()
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
