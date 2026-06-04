using PlayMe.Application.Abstractions;
using PlayMe.Application.Dtos;
using PlayMe.Domain.Platform;

namespace PlayMe.Application.Mapping;

/// <summary>
/// Wire-boundary projection for hidden-state games (Sprint 10 seam A).
/// <see cref="RoomMapper"/> always produces the full-state
/// <see cref="RoomDto"/> (it also feeds persistence-adjacent paths); the
/// Api layer runs every outgoing room payload through
/// <see cref="ForViewer"/>, which rewrites <c>CurrentMatch.State</c> with
/// the module's <see cref="IHiddenStateGame.SerializeFor"/> projection
/// when — and only when — the module opts in and the match is still live.
///
/// For every current module (no <see cref="IHiddenStateGame"/>), for
/// rooms without a match, and for terminal matches, the input instance is
/// returned unchanged — zero behavioral and zero allocation cost.
/// </summary>
public static class RoomViewProjector
{
    /// <summary>
    /// True when <paramref name="room"/> carries live hidden-state game
    /// state, i.e. its outgoing payloads differ per viewer and must be
    /// delivered per role rather than broadcast to the whole room group.
    /// </summary>
    public static bool RequiresProjection(RoomDto room, IGameModuleRegistry games) =>
        room.CurrentMatch is { Outcome: null }
        && games.GetModule(room.GameId) is IHiddenStateGame;

    /// <summary>
    /// Project <paramref name="room"/> for one viewer. <paramref name="viewer"/>
    /// is the receiving player's role, or <c>null</c> for a viewer with no
    /// seat in the room (the anonymous GET /api/rooms/{code} snapshot) —
    /// the module's public view. Viewer roles map to sides via the room's
    /// resolved side assignment; an unresolved side (possible only before
    /// a match exists) degrades to the public view.
    /// </summary>
    public static RoomDto ForViewer(RoomDto room, Role? viewer, IGameModuleRegistry games)
    {
        if (room.CurrentMatch is not { Outcome: null } match)
        {
            return room;
        }

        var module = games.GetModule(room.GameId);
        if (module is not IHiddenStateGame hidden)
        {
            return room;
        }

        var viewerSide = viewer switch
        {
            Role.Host => room.Host.Side,
            Role.Challenger => room.Challenger?.Side,
            _ => null,
        };

        var projected = hidden.SerializeFor(module.Deserialize(match.State), viewerSide);
        return room with { CurrentMatch = match with { State = projected } };
    }
}
