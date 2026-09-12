using System.Collections.Concurrent;
using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Game.Services;
using Acorn.Net;
using Acorn.World.Npc;
using Acorn.World.Services.Map;
using Acorn.World.Services.Npc;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.World.Map;

/// <summary>
///     Represents an item on the map with protection timer
/// </summary>
public class MapItem
{
    public required int Id { get; set; }
    public required int Amount { get; set; }
    public required Coords Coords { get; set; }
    public int OwnerId { get; set; }
    public int ProtectedTicks { get; set; }

    /// <summary>
    ///     Tick count when this item was dropped on the ground.
    ///     Used for ground item cleanup after expiry.
    /// </summary>
    public int DroppedAtTick { get; set; }
}
