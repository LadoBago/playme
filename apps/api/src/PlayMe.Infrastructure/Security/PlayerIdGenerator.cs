using System.Buffers.Text;
using System.Security.Cryptography;
using PlayMe.Application.Abstractions;
using PlayMe.Domain.Platform;

namespace PlayMe.Infrastructure.Security;

/// <summary>
/// Cryptographic <see cref="PlayerId"/> generator (CLAUDE.md §5.4). Same
/// primitive as <see cref="RoomCodeGenerator"/> — separate type so test
/// doubles can substitute independently.
/// </summary>
public sealed class PlayerIdGenerator : IPlayerIdGenerator
{
    private const int EntropyBytes = 16; // 128 bits

    public PlayerId NewPlayerId()
    {
        Span<byte> bytes = stackalloc byte[EntropyBytes];
        RandomNumberGenerator.Fill(bytes);
        return new PlayerId(Base64Url.EncodeToString(bytes));
    }
}
