using Microsoft.Extensions.DependencyInjection;

namespace PlayMe.Api.DependencyInjection;

public static class DomainServiceCollectionExtensions
{
    /// <summary>
    /// Registers Domain-layer services. Sprint 0 placeholder — Domain is pure
    /// C# and has nothing to register yet; rules engines (per-game) and
    /// platform invariants land in later sprints.
    /// </summary>
    public static IServiceCollection AddDomain(this IServiceCollection services) => services;
}
