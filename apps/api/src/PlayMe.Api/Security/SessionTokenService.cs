using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using PlayMe.Application.Abstractions;
using PlayMe.Domain.Platform;

namespace PlayMe.Api.Security;

/// <summary>
/// Signs (and encrypts) the session payload via ASP.NET Core's Data
/// Protection API (CLAUDE.md §5.4: "Payload signed via ASP.NET Core's Data
/// Protection API or a JWT — either is fine"). The DP key ring rotates
/// transparently and survives restarts in single-instance dev; in prod the
/// key ring is persisted via the default file-system store under the app's
/// data directory.
///
/// Token payload is a small JSON document carrying <c>roomCode</c>,
/// <c>playerId</c>, <c>role</c>, and <c>exp</c> (unix seconds). The DP-
/// protected blob is what goes into the cookie.
/// </summary>
public sealed class SessionTokenService : ISessionTokenService
{
    private const string ProtectorPurpose = "playme.session.v1";

    private readonly IDataProtector _protector;
    private readonly IClock _clock;
    private readonly TimeSpan _ttl;

    public SessionTokenService(
        IDataProtectionProvider dataProtection,
        IClock clock,
        TimeSpan ttl)
    {
        _protector = dataProtection.CreateProtector(ProtectorPurpose);
        _clock = clock;
        _ttl = ttl;
    }

    public string Mint(Session session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var payload = new SessionPayload(
            RoomCode: session.RoomCode.Value,
            PlayerId: session.PlayerId.Value,
            Role: session.Role.ToString(),
            Exp: _clock.UtcNow.Add(_ttl).ToUnixTimeSeconds());

        var json = JsonSerializer.Serialize(payload);
        return _protector.Protect(json);
    }

    public Session? TryParse(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        string json;
        try
        {
            json = _protector.Unprotect(token);
        }
        catch
        {
            // Tampered, wrong key ring, or otherwise unprotectable — treat
            // as an unauthenticated request, not a 500.
            return null;
        }

        SessionPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<SessionPayload>(json);
        }
        catch (JsonException)
        {
            return null;
        }
        if (payload is null) return null;

        if (DateTimeOffset.FromUnixTimeSeconds(payload.Exp) <= _clock.UtcNow)
        {
            return null;
        }

        if (!Enum.TryParse<Role>(payload.Role, ignoreCase: true, out var role))
        {
            return null;
        }

        try
        {
            return new Session(
                new RoomCode(payload.RoomCode),
                new PlayerId(payload.PlayerId),
                role);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private sealed record SessionPayload(string RoomCode, string PlayerId, string Role, long Exp);
}
