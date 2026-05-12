using Microsoft.AspNetCore.Mvc;

namespace PlayMe.Api.Controllers;

/// <summary>
/// One-off Sentry smoke-test endpoint. Hit once after wiring the DSN,
/// confirm the error appears in Sentry, then delete this controller.
/// Not for production — drop before Sprint 7.
/// </summary>
[ApiController]
[Route("api/debug")]
public sealed class DebugController : ControllerBase
{
    [HttpGet("sentry-test")]
    public IActionResult ThrowForSentry()
    {
        throw new InvalidOperationException("PlayMe Sentry smoke test (server, unhandled)");
    }
}
