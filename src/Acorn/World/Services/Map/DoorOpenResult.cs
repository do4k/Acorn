using Acorn.Game.Models;
using Acorn.Net;
using Acorn.World.Map;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.World.Services.Map;

/// <summary>
///     Outcome of validating a door-open request.
/// </summary>
public enum DoorOpenResult
{
    /// <summary>The door exists and the player may open it.</summary>
    Opened,

    /// <summary>There is no door at the requested coordinates (or they are out of bounds).</summary>
    NotADoor,

    /// <summary>The door is already open.</summary>
    AlreadyOpen,

    /// <summary>The player is not close enough to interact with the door.</summary>
    OutOfRange,

    /// <summary>The door is locked and the player does not hold the required key.</summary>
    Locked
}
