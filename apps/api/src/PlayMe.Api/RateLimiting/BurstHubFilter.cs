using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using PlayMe.Application.Errors;

namespace PlayMe.Api.RateLimiting;

/// <summary>
/// SignalR hub filter that enforces a per-connection burst ceiling
/// (<c>docs/security.md §5</c>: "≤ 20 messages/sec hard ceiling per
/// connection"). State lives in process keyed by <c>ConnectionId</c> and
/// dies with the connection — which is correct for a burst limit. A
/// per-session quota (e.g. 60 moves/min) must use the Application-layer
/// <c>IRateLimiter</c> port and Redis so it survives reconnects.
/// </summary>
public sealed class BurstHubFilter : IHubFilter
{
    private const int MaxMessagesPerWindow = 20;
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(1);

    private readonly ConcurrentDictionary<string, ConnectionWindow> _windows = new();

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var key = invocationContext.Context.ConnectionId;
        var window = _windows.GetOrAdd(key, _ => new ConnectionWindow());

        if (!window.TryConsume(DateTimeOffset.UtcNow))
        {
            throw new HubException(PlatformErrors.RateExceeded);
        }

        return await next(invocationContext).ConfigureAwait(false);
    }

    public Task OnDisconnectedAsync(
        HubLifetimeContext context,
        Exception? exception,
        Func<HubLifetimeContext, Exception?, Task> next)
    {
        _windows.TryRemove(context.Context.ConnectionId, out _);
        return next(context, exception);
    }

    /// <summary>Fixed-window counter; cheap and good enough for a burst ceiling.</summary>
    private sealed class ConnectionWindow
    {
        private readonly Lock _gate = new();
        private DateTimeOffset _windowStart;
        private int _count;

        public bool TryConsume(DateTimeOffset now)
        {
            lock (_gate)
            {
                if (now - _windowStart >= Window)
                {
                    _windowStart = now;
                    _count = 0;
                }
                if (_count >= MaxMessagesPerWindow)
                {
                    return false;
                }
                _count++;
                return true;
            }
        }
    }
}
