using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace PlayMe.Api.Security;

/// <summary>
/// Reads the session cookie off any <see cref="HttpRequest"/> (controller
/// or SignalR negotiate) and returns the validated <see cref="Session"/>.
/// </summary>
public sealed class SessionCookieReader
{
    private readonly ISessionTokenService _tokens;
    private readonly IOptionsMonitor<SessionCookieOptions> _options;

    public SessionCookieReader(
        ISessionTokenService tokens,
        IOptionsMonitor<SessionCookieOptions> options)
    {
        _tokens = tokens;
        _options = options;
    }

    public Session? Read(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = _options.CurrentValue.Name;
        if (!request.Cookies.TryGetValue(name, out var token))
        {
            return null;
        }
        return _tokens.TryParse(token);
    }
}
