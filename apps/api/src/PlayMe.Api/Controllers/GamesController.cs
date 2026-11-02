using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PlayMe.Api.RateLimiting;
using PlayMe.Application.Dtos;
using PlayMe.Application.Queries.GetGameCatalog;

namespace PlayMe.Api.Controllers;

/// <summary>
/// Server-derived game catalog (Sprint 12 seam D). The web's static
/// catalog owns names, rules text and artwork; this endpoint answers only
/// what the server alone can: which registered modules can play one side
/// themselves. Anonymous and side-effect free — no session, no room.
/// </summary>
[ApiController]
[Route("api/games")]
public sealed class GamesController : ControllerBase
{
    private readonly GetGameCatalogHandler _catalog;

    public GamesController(GetGameCatalogHandler catalog)
    {
        _catalog = catalog;
    }

    [HttpGet]
    [EnableRateLimiting(RateLimitingPolicies.GamesList)]
    public ActionResult<IReadOnlyList<GameCatalogEntryDto>> List() => Ok(_catalog.Handle());
}
