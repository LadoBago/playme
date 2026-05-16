using Microsoft.AspNetCore.Http;

namespace PlayMe.Api.Security;

/// <summary>
/// Configuration for the session cookie (CLAUDE.md §5.4). Bound to the
/// <c>SessionCookie</c> section of appsettings; default values work for
/// local dev (no Domain, <see cref="SameSiteMode.Lax"/>, 6 h lifespan).
/// Production deployments override:
/// <list type="bullet">
///   <item>
///   <see cref="Domain"/> with <c>"playme.ge"</c> when the API is on a
///   <c>*.playme.ge</c> subdomain — shares the cookie across subdomains.
///   </item>
///   <item>
///   <see cref="SameSite"/> with <see cref="SameSiteMode.None"/> when the
///   API is on a different site than the web (e.g. the API is hosted at
///   <c>*.azurewebsites.net</c> while the web is at <c>www.playme.ge</c>).
///   Without this, the browser drops the cookie on every cross-site call
///   and the host loses their session immediately after creating a room.
///   <see cref="SameSiteMode.None"/> requires <c>Secure=true</c>; we set
///   that automatically outside Development.
///   </item>
/// </list>
/// </summary>
public sealed class SessionCookieOptions
{
    public const string SectionName = "SessionCookie";

    public string Name { get; set; } = "playme.session";

    /// <summary>
    /// Cookie domain. Null → host-only (dev default). When the API and web
    /// share an eTLD+1 (e.g. <c>api.playme.ge</c> + <c>www.playme.ge</c>)
    /// set this to <c>playme.ge</c> so the cookie applies to both — via
    /// <c>SessionCookie__Domain=playme.ge</c> as an env var or the matching
    /// key in <c>appsettings.Production.json</c>. Leave null when the API
    /// is on a different eTLD+1 (the browser would reject a Domain that
    /// doesn't match the response origin).
    /// </summary>
    public string? Domain { get; set; }

    /// <summary>
    /// SameSite attribute on the cookie. Defaults to
    /// <see cref="SameSiteMode.Lax"/>, which is the right call when the
    /// API and web share an eTLD+1. Set to <see cref="SameSiteMode.None"/>
    /// in cross-site deployments — required for the browser to attach the
    /// cookie to fetches and SignalR upgrades from a different origin.
    /// </summary>
    public SameSiteMode SameSite { get; set; } = SameSiteMode.Lax;

    public TimeSpan MaxAge { get; set; } = TimeSpan.FromHours(6);
}
