using Microsoft.Extensions.DependencyInjection;

namespace PlayMe.Api.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers Application handlers, ports, and validators.
    /// Sprint 0 placeholder — handlers (CreateRoom / JoinRoom / SubmitMove)
    /// arrive in Sprint 1.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services) => services;
}
