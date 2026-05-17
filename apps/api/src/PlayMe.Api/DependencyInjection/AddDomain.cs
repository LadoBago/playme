using Microsoft.Extensions.DependencyInjection;
using PlayMe.Domain.Games.Connect4;
using PlayMe.Domain.Games.TicTacToe3x3;
using PlayMe.Domain.Games.TicTacToe6x6;
using PlayMe.Domain.Platform;

namespace PlayMe.Api.DependencyInjection;

public static class DomainServiceCollectionExtensions
{
    /// <summary>
    /// Registers Domain-layer game modules as <see cref="IGameModule"/>.
    /// Adding a new game (Sprints 3–4) is purely additive here per
    /// CLAUDE.md §2.3 / §8 SOLID open-closed.
    /// </summary>
    public static IServiceCollection AddDomain(this IServiceCollection services)
    {
        services.AddSingleton<IGameModule, TicTacToe3x3GameModule>();
        services.AddSingleton<IGameModule, TicTacToe6x6GameModule>();
        services.AddSingleton<IGameModule, Connect4GameModule>();
        return services;
    }
}
