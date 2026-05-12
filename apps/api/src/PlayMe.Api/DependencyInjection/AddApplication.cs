using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Commands.CreateRoom;
using PlayMe.Application.Commands.JoinRoom;
using PlayMe.Application.Commands.RegisterPresence;
using PlayMe.Application.Commands.ReleasePresence;
using PlayMe.Application.Commands.SubmitMove;
using PlayMe.Application.Games.TicTacToe3x3;
using PlayMe.Application.Queries.GetRoom;

namespace PlayMe.Api.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers Application handlers, ports, validators, and per-game
    /// move parsers (CLAUDE.md §2.4 dependency rule: ports defined here,
    /// implementations live in Infrastructure / DI extension).
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

        // Validators — discovered via assembly scan of the Application asm.
        services.AddValidatorsFromAssemblyContaining<CreateRoomCommandValidator>();

        // Per-game move parsers.
        services.AddSingleton<IGameMoveParser, TicTacToeMoveParser>();

        return services;
    }
}
