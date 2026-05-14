using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PlayMe.Application.Abstractions;

namespace PlayMe.Api.Security;

/// <summary>
/// Helper that issues the signed session cookie on an outgoing response with
/// the attributes from CLAUDE.md §5.4: HttpOnly, Secure (in non-dev),
/// SameSite=Lax, Path=/, configurable Domain and lifetime.
/// </summary>
public sealed class SessionCookieWriter
{
    private readonly ISessionTokenService _tokens;
    private readonly IOptionsMonitor<SessionCookieOptions> _options;
    private readonly IClock _clock;
    private readonly IHostEnvironment _env;

    public SessionCookieWriter(
        ISessionTokenService tokens,
        IOptionsMonitor<SessionCookieOptions> options,
        IClock clock,
        IHostEnvironment env)
    {
        _tokens = tokens;
        _options = options;
        _clock = clock;
        _env = env;
    }

    public void Write(HttpResponse response, Session session)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(session);

        var token = _tokens.Mint(session);
        var opts = _options.CurrentValue;

        response.Cookies.Append(opts.Name, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = !_env.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Domain = opts.Domain,
            MaxAge = opts.MaxAge,
            Expires = _clock.UtcNow.Add(opts.MaxAge),
        });
    }
}
