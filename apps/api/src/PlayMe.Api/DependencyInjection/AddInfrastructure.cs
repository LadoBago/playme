using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlayMe.Application.Abstractions;
using PlayMe.Infrastructure.Time;
using StackExchange.Redis;

namespace PlayMe.Api.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Wires Infrastructure adapters: Redis multiplexer, system clock, telemetry.
    /// Redis powers both the state store and the SignalR backplane
    /// (CLAUDE.md §2.1, §6).
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IClock, SystemClock>();

        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? "localhost:6379";

        // abortConnect=false: retry quietly if Redis isn't reachable at startup.
        // Production prod-readiness: a real liveness/readiness check (Sprint 7)
        // covers the "Redis went away" case explicitly.
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var options = ConfigurationOptions.Parse(redisConnectionString);
            options.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(options);
        });

        return services;
    }
}
