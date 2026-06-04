using Microsoft.Extensions.DependencyInjection;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Commands.AcceptRematch;
using PlayMe.Application.Commands.AdjudicateDisconnectGrace;
using PlayMe.Application.Commands.AdjudicatePostMatchExitGrace;
using PlayMe.Application.Commands.AdjudicateRoomExpiry;
using PlayMe.Application.Commands.AdjudicateSetupTimeout;
using PlayMe.Application.Commands.AdjudicateTimeout;
using PlayMe.Application.Commands.CreateRoom;
using PlayMe.Application.Commands.ExitRoom;
using PlayMe.Application.Commands.JoinRoom;
using PlayMe.Application.Commands.OfferRematch;
using PlayMe.Application.Commands.RegisterPresence;
using PlayMe.Application.Commands.RejectRematch;
using PlayMe.Application.Commands.ReleasePresence;
using PlayMe.Application.Commands.Resign;
using PlayMe.Application.Commands.SubmitMove;
using PlayMe.Application.Commands.SubmitSetup;
using PlayMe.Application.Games.Connect4;
using PlayMe.Application.Games.Reversi;
using PlayMe.Application.Games.TicTacToe;
using PlayMe.Application.Queries.GetRoom;
using PlayMe.Application.Time;

namespace PlayMe.Api.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers Application handlers, ports, and per-game move parsers
    /// (CLAUDE.md §2.4 dependency rule: ports defined here, implementations
    /// live in Infrastructure / DI extension).
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Handlers — scoped so each request gets its own with fresh deps.
        services.AddScoped<CreateRoomHandler>();
        services.AddScoped<JoinRoomHandler>();
        services.AddScoped<GetRoomHandler>();
        services.AddScoped<RegisterPresenceHandler>();
        services.AddScoped<ReleasePresenceHandler>();
        services.AddScoped<SubmitMoveHandler>();
        services.AddScoped<SubmitSetupHandler>();
        services.AddScoped<ResignHandler>();
        services.AddScoped<ExitRoomHandler>();
        services.AddScoped<OfferRematchHandler>();
        services.AddScoped<AcceptRematchHandler>();
        services.AddScoped<RejectRematchHandler>();
        services.AddScoped<AdjudicateTimeoutHandler>();
        services.AddScoped<AdjudicateSetupTimeoutHandler>();
        services.AddScoped<AdjudicateDisconnectGraceHandler>();
        services.AddScoped<AdjudicatePostMatchExitGraceHandler>();
        services.AddScoped<AdjudicateRoomExpiryHandler>();

        // Pure-compute clock facade. Singleton — stateless.
        services.AddSingleton<IClockService, ClockService>();

        // Per-game move parsers.
        services.AddSingleton<IGameMoveParser, TicTacToeMoveParser>();
        services.AddSingleton<IGameMoveParser, Connect4MoveParser>();
        services.AddSingleton<IGameMoveParser, ReversiMoveParser>();

        return services;
    }
}
