using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace PlayMe.Api.RateLimiting;

/// <summary>
/// Wires the per-IP HTTP rate-limit policies enumerated in
/// <c>docs/security.md §5</c>. The SignalR per-connection burst ceiling
/// is registered separately via <see cref="BurstHubFilter"/>; per-session
/// quotas (move flood, rematch spam) belong to the Application layer
/// behind the <c>IRateLimiter</c> port and are wired alongside the
/// SubmitMove pipeline.
/// </summary>
public static class RateLimitingServiceCollectionExtensions
{
    public static IServiceCollection AddPlayMeRateLimiting(this IServiceCollection services)
    {
        // Permit counts come from configuration (defaults in the options
        // class match docs/security.md §5) so a load-test window can widen
        // them without a redeploy — see IpRateLimitingOptions.
        services.AddOptions<IpRateLimitingOptions>()
            .BindConfiguration(IpRateLimitingOptions.SectionName);

        services.AddRateLimiter(options =>
        {
            // 429 with a JSON body (HTML-free; the API never serves HTML).
            // Retry-After is set per RFC 6585 so well-behaved clients back off.
            options.RejectionStatusCode = (int)HttpStatusCode.TooManyRequests;
            options.OnRejected = static async (context, token) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                await context.HttpContext.Response.WriteAsync(
                    "{\"code\":\"errors.rate.exceeded\"}",
                    token).ConfigureAwait(false);
            };

            options.AddPolicy(RateLimitingPolicies.RoomsCreate,
                ctx => RateLimitPartition.GetFixedWindowLimiter(
                    PartitionKey(ctx),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = Limits(ctx).RoomsCreatePermitLimit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));

            options.AddPolicy(RateLimitingPolicies.RoomsJoin,
                ctx => RateLimitPartition.GetFixedWindowLimiter(
                    PartitionKey(ctx),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = Limits(ctx).RoomsJoinPermitLimit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));

            // GetRoom backs the pre-session invite preview, so it's the only
            // anonymous read in the controller surface. The room code itself
            // is the access token (128-bit CSPRNG ⇒ enumeration intractable);
            // this limit caps the abuse window if a code leaks elsewhere.
            options.AddPolicy(RateLimitingPolicies.RoomsGet,
                ctx => RateLimitPartition.GetFixedWindowLimiter(
                    PartitionKey(ctx),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = Limits(ctx).RoomsGetPermitLimit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));
        });

        return services;
    }

    /// <summary>
    /// Resolve the configured permit limits for this request. Read per
    /// partition creation (the factory lambda runs once per new partition
    /// key), so the cost is a scoped service lookup on first sight of an
    /// IP — not per request.
    /// </summary>
    private static IpRateLimitingOptions Limits(HttpContext ctx) =>
        ctx.RequestServices.GetRequiredService<IOptions<IpRateLimitingOptions>>().Value;

    /// <summary>
    /// Partition key for per-IP policies. Prefers
    /// <see cref="HttpContext.Connection"/>'s remote IP; falls back to a
    /// constant when the IP isn't available (loopback in tests) so the
    /// limiter still partitions deterministically.
    /// </summary>
    private static string PartitionKey(HttpContext ctx)
    {
        var ip = ctx.Connection.RemoteIpAddress;
        if (ip is null) return "unknown";
        // Normalise IPv4-mapped IPv6 so a single client doesn't land in
        // two partitions depending on socket type.
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        return ip.ToString();
    }
}
