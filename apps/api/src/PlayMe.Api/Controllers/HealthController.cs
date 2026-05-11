using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using PlayMe.Application.Abstractions;

namespace PlayMe.Api.Controllers;

/// <summary>
/// Trivial health endpoint used by the web landing page in Sprint 0
/// (CLAUDE.md §11) and by uptime checks thereafter.
/// </summary>
[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    private readonly IClock _clock;

    public HealthController(IClock clock)
    {
        _clock = clock;
    }

    [HttpGet]
    public ActionResult<HealthResponse> Get()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "0.0.0";

        return Ok(new HealthResponse(
            Status: "ok",
            Service: "playme-api",
            Version: version,
            Timestamp: _clock.UtcNow));
    }
}

public sealed record HealthResponse(
    string Status,
    string Service,
    string Version,
    DateTimeOffset Timestamp);
