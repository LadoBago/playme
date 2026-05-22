using Microsoft.Extensions.DependencyInjection;
using PlayMe.Domain.Games.Connect4;
using PlayMe.Domain.Games.Reversi;
using PlayMe.Domain.Games.TicTacToe;
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
        services.AddSingleton<IGameModule, TicTacToeGameModule>();
        services.AddSingleton<IGameModule, Connect4GameModule>();
        services.AddSingleton<IGameModule, ReversiGameModule>();
        return services;
    }
}
