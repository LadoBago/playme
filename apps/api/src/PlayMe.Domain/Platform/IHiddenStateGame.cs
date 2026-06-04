namespace PlayMe.Domain.Platform;

/// <summary>
/// Optional capability for game modules whose state contains hidden
/// information (Sprint 10 seam A; see docs/games/seabattle.md). A module
/// implements this <em>in addition to</em> <see cref="IGameModule"/>; the
/// platform dispatches by capability (<c>module is IHiddenStateGame</c>),
/// never by <see cref="GameId"/>.
///
/// The projection is consulted only at the wire boundary and only while
/// the match has no <see cref="Outcome"/> — persistence always stores the
/// full <see cref="IGameModule.Serialize"/> blob, and once a match is
/// terminal (win, draw, resign, timeout, disconnect) both players receive
/// the full unprojected state. Modules that don't implement this interface
/// are unaffected: their single serialization is broadcast to both players
/// exactly as before.
/// </summary>
public interface IHiddenStateGame
{
    /// <summary>
    /// Wire-facing projection of <paramref name="state"/> for one viewer.
    /// <paramref name="viewerSide"/> is one of
    /// <see cref="IGameModule.ValidSides"/>, or <c>null</c> for a viewer
    /// with no side (anonymous room snapshot today, spectators if they
    /// ever land) — the module decides what its public view exposes. The
    /// shape of the projected blob is a module ↔ renderer agreement; the
    /// platform never inspects it (CLAUDE.md §7 "Platform thinness").
    /// </summary>
    string SerializeFor(IGameState state, string? viewerSide);
}
