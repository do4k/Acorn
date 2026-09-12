using Acorn.Game.Models;
using Acorn.Net;
using Acorn.World.Map;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.World.Services.Map;

/// <summary>
///     Handles opening map doors, including validation and broadcasting the
///     Door/Open packet to nearby players.
/// </summary>
public interface IDoorService
{
    /// <summary>
    ///     Validates whether <paramref name="character" /> may open the door at
    ///     <paramref name="coords" /> on <paramref name="map" />.
    ///     When the result is <see cref="DoorOpenResult.Locked" />,
    ///     <paramref name="requiredKey" /> is set to the key/door spec the client expects.
    /// </summary>
    DoorOpenResult ValidateDoorOpen(Character character, Coords coords, MapState map, out int requiredKey);

    /// <summary>
    ///     Attempts to open the door at <paramref name="coords" />. Broadcasts
    ///     Door/Open to in-range players on success, or sends the Door/Close locked
    ///     reply to the requesting player when the door is locked.
    /// </summary>
    Task<bool> OpenDoorAsync(PlayerState player, Coords coords, MapState map);
}
