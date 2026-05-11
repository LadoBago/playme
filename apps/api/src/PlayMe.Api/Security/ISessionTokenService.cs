namespace PlayMe.Api.Security;

/// <summary>
/// Encapsulates minting and parsing of the signed session cookie token.
/// The token is opaque to the client; only the server can decode it
/// (CLAUDE.md §5.4).
/// </summary>
public interface ISessionTokenService
{
    /// <summary>
    /// Produce a signed, expiry-bounded token for <paramref name="session"/>.
    /// The string is safe to set as a cookie value (URL/base64 friendly).
    /// </summary>
    string Mint(Session session);

    /// <summary>
    /// Validate the signature + expiry of <paramref name="token"/> and return
    /// the decoded <see cref="Session"/>, or null if the token is missing,
    /// malformed, tampered, or expired.
    /// </summary>
    Session? TryParse(string? token);
}
