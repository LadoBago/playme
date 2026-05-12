using System.Buffers.Text;
using System.Security.Cryptography;
using PlayMe.Application.Abstractions;
using PlayMe.Domain.Platform;

namespace PlayMe.Infrastructure.Security;

/// <summary>
/// Cryptographic <see cref="RoomCode"/> generator (CLAUDE.md §5.4):
/// 128 random bits, URL-safe base64 (no padding). Never <c>Guid.NewGuid</c>,
/// never time-derived.
/// </summary>
public sealed class RoomCodeGenerator : IRoomCodeGenerator
{
    private const int EntropyBytes = 16; // 128 bits

    public RoomCode NewCode()
    {
        Span<byte> bytes = stackalloc byte[EntropyBytes];
        RandomNumberGenerator.Fill(bytes);
        return new RoomCode(Base64Url.EncodeToString(bytes));
    }
}
