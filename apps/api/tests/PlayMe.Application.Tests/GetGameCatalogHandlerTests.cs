using FluentAssertions;
using PlayMe.Application.Abstractions;
using PlayMe.Application.Queries.GetGameCatalog;
using PlayMe.Application.Tests.Fakes;
using PlayMe.Domain.Platform;
using Xunit;

namespace PlayMe.Application.Tests;

/// <summary>
/// Seam D (Sprint 12): the catalog derives <c>supportsBot</c> from each
/// registered module's interfaces, in registration order, with no list
/// to maintain.
/// </summary>
public sealed class GetGameCatalogHandlerTests
{
    private sealed class ManyGamesRegistry : IGameModuleRegistry
    {
        private readonly List<IGameModule> _modules;

        public ManyGamesRegistry(params IGameModule[] modules)
        {
            _modules = modules.ToList();
        }

        public IReadOnlyCollection<GameId> GameIds => _modules.Select(m => m.Id).ToList();

        public bool IsRegistered(GameId id) => _modules.Any(m => m.Id == id);

        public IGameModule GetModule(GameId id) => _modules.Single(m => m.Id == id);

        public IGameMoveParser GetMoveParser(GameId id) => throw new NotSupportedException();
    }

    [Fact]
    public void Flags_come_from_the_module_interfaces_in_registration_order()
    {
        var registry = new ManyGamesRegistry(
            new FakeKeepTurnModule(),
            new FakeBotGameModule(),
            new FakeSetupGameModule(),
            new FakeBotSetupGameModule());

        var catalog = new GetGameCatalogHandler(registry).Handle();

        catalog.Select(e => (e.Id.Value, e.SupportsBot)).Should().Equal(
            (FakeKeepTurnModule.Id_.Value, false),
            (FakeBotGameModule.Id_.Value, true),
            (FakeSetupGameModule.Id_.Value, false),
            (FakeBotSetupGameModule.SetupId.Value, true));
    }
}
