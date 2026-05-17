using System.Security.Cryptography;
using System.Text;
using PlayMe.Application.Abstractions;

namespace PlayMe.Infrastructure.Security;

/// <summary>
/// SHA-256-based <see cref="IRoomCodeRedactor"/>: hashes the room code and
/// emits the first 32 bits as 8 lowercase hex characters prefixed with
/// <c>rc:</c> — enough to correlate log lines on the same room without
/// exposing the raw token (docs/security.md §8). Stateless and
/// deterministic by design; no secret salt — the threat we mitigate is
/// raw codes sitting in stored log files, not an attacker reproducing the
/// hash.
/// </summary>
public sealed class RoomCodeRedactor : IRoomCodeRedactor
{
    private const string Prefix = "rc:";

    /// <summary>Number of leading SHA-256 bytes to include (32 bits).</summary>
    private const int DigestBytes = 4;

    public string Redact(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        var byteCount = Encoding.UTF8.GetByteCount(code);
        Span<byte> input = byteCount <= 256 ? stackalloc byte[byteCount] : new byte[byteCount];
        Encoding.UTF8.GetBytes(code, input);

        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(input, digest);

        return string.Concat(Prefix, Convert.ToHexString(digest[..DigestBytes]).ToLowerInvariant());
    }
}
