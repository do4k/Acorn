using Acorn.Net;
using Acorn.World.Map;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.World.Services.Map;

/// <summary>
///     Service for broadcasting visual and sound effects on a map.
/// </summary>
public interface IMapEffectService
{
    /// <summary>
    ///     Play a visual/sound effect at specific map coordinates.
    ///     Only sent to players in client range of any of the specified coordinates.
    /// </summary>
    Task EffectOnCoordsAsync(MapState map, IReadOnlyList<Coords> coords, int effectId);

    /// <summary>
    ///     Play a visual effect on specific players.
    ///     Only sent to players in client range of any of the target players.
    /// </summary>
    Task EffectOnPlayersAsync(MapState map, IReadOnlyList<int> playerIds, int effectId);

    /// <summary>
    ///     Trigger a map-wide quake effect with the specified magnitude (1-8).
    /// </summary>
    Task QuakeAsync(MapState map, int magnitude);
}
