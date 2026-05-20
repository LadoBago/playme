using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PlayMe.Application.Abstractions;
using PlayMe.Infrastructure.Games;
using PlayMe.Infrastructure.RateLimiting;
using PlayMe.Infrastructure.Random;
using PlayMe.Infrastructure.Redis;
using PlayMe.Infrastructure.Scheduling;
using PlayMe.Infrastructure.Security;
using PlayMe.Infrastructure.Telemetry;
using PlayMe.Infrastructure.Time;
using StackExchange.Redis;

namespace PlayMe.Api.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Wires Infrastructure adapters: Redis (state store + SignalR backplane
    /// per CLAUDE.md §2.1, §6), the system clock, cryptographic RNGs for
    /// room codes and player IDs (§5.4), and the game-module registry.
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

        services.AddSingleton<IRoomRepository, RedisRoomRepository>();
        services.AddSingleton<IRoomCodeGenerator, RoomCodeGenerator>();
        services.AddSingleton<IRoomCodeRedactor, RoomCodeRedactor>();
        services.AddSingleton<IPlayerIdGenerator, PlayerIdGenerator>();
        services.AddSingleton<IRandom, SystemRandom>();
        services.AddSingleton<IGameModuleRegistry, GameModuleRegistry>();
        services.AddSingleton<IRateLimiter, RedisRateLimiter>();

        // Sprint 2 PR #2: Redis-backed sorted-set schedulers + BackgroundService
        // sweepers per state.md §2.2. Schedulers are stateless (just ZADD/ZREM
        // wrappers); sweepers run one instance per API process and dispatch
        // adjudication handlers under the per-room distributed lock.
        services.Configure<SweeperOptions>(configuration.GetSection("Sweepers"));
        services.AddSingleton<ITimeoutScheduler, RedisTimeoutScheduler>();
        services.AddSingleton<IDisconnectGraceScheduler, RedisDisconnectGraceScheduler>();
        services.AddSingleton<RedisTimeoutSweeperService>();
        services.AddSingleton<RedisDisconnectGraceSweeperService>();
        services.AddHostedService(sp => sp.GetRequiredService<RedisTimeoutSweeperService>());
        services.AddHostedService(sp => sp.GetRequiredService<RedisDisconnectGraceSweeperService>());

        // Sprint 7: server-side product analytics (docs/observability-and-i18n.md
        // §1.2). Authoritative outcomes — match_ended, room_expired — fire from
        // the API so the catalog stays accurate when a client disconnects before
        // it can report. The web emits user-action events only.
        //
        // PostHog:ApiKey empty / unset → NoOp adapter. Local dev, integration
        // tests, and any environment without a key get silent dropping; no
        // accidental events from developer machines into a shared project.
        services.Configure<PostHogOptions>(configuration.GetSection("PostHog"));
        var posthogKey = configuration["PostHog:ApiKey"];
        if (string.IsNullOrWhiteSpace(posthogKey))
        {
            services.AddSingleton<IAnalyticsClient, NoOpAnalyticsClient>();
        }
        else
        {
            services.AddHttpClient<IAnalyticsClient, PostHogAnalyticsClient>((sp, http) =>
            {
                var opts = sp.GetRequiredService<IOptions<PostHogOptions>>().Value;
                http.BaseAddress = new Uri(opts.Host);
                // 5s ceiling so a slow/down capture endpoint can't queue
                // up against gameplay traffic. The adapter logs + swallows
                // timeouts so missing the event is acceptable; blocking
                // is not.
                http.Timeout = TimeSpan.FromSeconds(5);
            });
        }

        return services;
    }
}
