using Microsoft.AspNetCore.Http;

namespace PlayMe.Api.Security;

/// <summary>
/// Configuration for the session cookie (CLAUDE.md §5.4). Bound to the
/// <c>SessionCookie</c> section of appsettings; defaults work for local
/// dev (no Domain, <see cref="SameSiteMode.Lax"/>, 6 h lifespan) and for
/// the current v1 production setup, where the API is at
/// <c>api.playme.ge</c> (Cloudflare → Azure) and the web at
/// <c>www.playme.ge</c>: same eTLD+1 means the cookie issued by the API
/// is host-only on <c>api.playme.ge</c>, and <see cref="SameSiteMode.Lax"/>
/// allows the browser to attach it on cross-subdomain same-site requests
/// from the web. No production overrides required.
///
/// The two settings below exist for the deployment topologies we don't
/// currently use but may need in the future — see their individual
/// summaries for when and how to set them.
/// </summary>
public sealed class SessionCookieOptions
{
    public const string SectionName = "SessionCookie";

    public string Name { get; set; } = "playme.session";

    /// <summary>
    /// Cookie domain. <c>null</c> → host-only on the issuing host, which
    /// is what we ship: the API issues a cookie scoped to
    /// <c>api.playme.ge</c>, and the browser sends it on same-site
    /// requests from the web. Only set this (e.g. to <c>playme.ge</c>)
    /// if you need a single cookie shared across multiple subdomains the
    /// API doesn't issue from — that isn't the case in v1.
    /// </summary>
    public string? Domain { get; set; }

    /// <summary>
    /// SameSite attribute on the cookie. Defaults to
    /// <see cref="SameSiteMode.Lax"/>, which is the right call when the
    /// API and web share an eTLD+1 (today's setup:
    /// <c>api.playme.ge</c> + <c>www.playme.ge</c>). Set to
    /// <see cref="SameSiteMode.None"/> only if the topology ever changes
    /// to cross-site (e.g. API on <c>*.azurewebsites.net</c> directly) —
    /// required so the browser attaches the cookie on cross-site fetches
    /// and SignalR upgrades. <see cref="SameSiteMode.None"/> requires
    /// <c>Secure=true</c>; we set that automatically outside Development.
    /// </summary>
    public SameSiteMode SameSite { get; set; } = SameSiteMode.Lax;

    public TimeSpan MaxAge { get; set; } = TimeSpan.FromHours(6);
}
