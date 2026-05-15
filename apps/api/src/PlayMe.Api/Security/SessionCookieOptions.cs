namespace PlayMe.Api.Security;

/// <summary>
/// Configuration for the session cookie (CLAUDE.md §5.4). Bound to the
/// <c>SessionCookie</c> section of appsettings; default values work for
/// local dev (no Domain, 6 h lifespan). Production deployments override
/// <see cref="Domain"/> with <c>"playme.ge"</c> so the cookie is shared
/// across subdomains.
/// </summary>
public sealed class SessionCookieOptions
{
    public const string SectionName = "SessionCookie";

    public string Name { get; set; } = "playme.session";

    /// <summary>
    /// Cookie domain. Null → host-only (dev default). In production this
    /// MUST be set to <c>playme.ge</c> via configuration —
    /// <c>SessionCookie__Domain=playme.ge</c> as an env var on Azure
    /// App Service, or the matching key in <c>appsettings.Production.json</c>
    /// — so the cookie applies to <c>api.playme.ge</c> too. Leaving it
    /// unset in prod silently breaks cross-subdomain auth.
    /// </summary>
    public string? Domain { get; set; }

    public TimeSpan MaxAge { get; set; } = TimeSpan.FromHours(6);
}
