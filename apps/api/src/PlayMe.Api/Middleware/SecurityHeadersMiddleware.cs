namespace PlayMe.Api.Middleware;

/// <summary>
/// Stamps the HTTP security-header set documented in
/// <c>docs/security.md §6</c> onto every API response.
///
/// The API never serves HTML — only JSON / SignalR negotiations — so the
/// CSP is the most-restrictive form possible: <c>default-src 'none'</c>
/// with <c>frame-ancestors 'none'</c>. A browser tricked into framing or
/// inline-loading an API response gets nothing.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(static state =>
        {
            var response = (HttpResponse)state;
            var headers = response.Headers;

            // Append-if-missing — `Append` would duplicate if a downstream
            // handler already wrote the header (Sentry, dev tooling).
            SetIfMissing(headers, "Content-Security-Policy",
                "default-src 'none'; frame-ancestors 'none'");
            SetIfMissing(headers, "X-Content-Type-Options", "nosniff");
            SetIfMissing(headers, "X-Frame-Options", "DENY");
            SetIfMissing(headers, "Referrer-Policy", "strict-origin-when-cross-origin");
            SetIfMissing(headers, "Strict-Transport-Security",
                "max-age=63072000; includeSubDomains; preload");
            SetIfMissing(headers, "Permissions-Policy",
                "camera=(), microphone=(), geolocation=(), usb=(), payment=(), accelerometer=(), magnetometer=()");

            return Task.CompletedTask;
        }, context.Response);

        return _next(context);
    }

    private static void SetIfMissing(IHeaderDictionary headers, string key, string value)
    {
        if (!headers.ContainsKey(key))
        {
            headers[key] = value;
        }
    }
}
