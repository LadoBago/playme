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
    /// Cookie domain. Null → host-only (dev default). In prod set to
    /// <c>playme.ge</c> so the cookie applies to <c>api.playme.ge</c> too.
    /// </summary>
    public string? Domain { get; set; }

    public TimeSpan MaxAge { get; set; } = TimeSpan.FromHours(6);
}
