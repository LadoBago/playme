using PlayMe.Domain.Platform;

namespace PlayMe.Application.Dtos;

/// <summary>
/// One row of <c>GET /api/games</c>: the capabilities the web needs to
/// gate its create form, derived from the registered module's interfaces
/// (Sprint 12 seam D; docs/bots.md §2). The static web catalog keeps the
/// names, rules text and artwork; this carries only what the server
/// alone can answer.
/// </summary>
/// <param name="Id">The game module id.</param>
/// <param name="SupportsBot">True when the module can play one side
/// itself — <c>IBotPlayer</c>, plus <c>IBotSetup</c> for setup games.</param>
public sealed record GameCatalogEntryDto(GameId Id, bool SupportsBot);
