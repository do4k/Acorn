# Prompt: Create New Packet Handler

Use this template when creating a new packet handler for the Acorn server.

## Instructions

Create a new packet handler that processes `{PacketName}` packets from the client.

### Requirements

1. **Location**: `src/Acorn/Net/PacketHandlers/{Category}/{PacketName}Handler.cs`
2. **Implements**: `IPacketHandler<{PacketName}>`
3. **Inject dependencies via primary constructor**

### Checklist

- [ ] Validate player state (character, map, session)
- [ ] Validate packet data
- [ ] Call appropriate game services
- [ ] Send response packet(s)
- [ ] Log important actions
- [ ] Handle edge cases gracefully

### Template

```csharp
using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.{Category};

public class {PacketName}Handler(
    ILogger<{PacketName}Handler> logger,
    I{ServiceName}Service {serviceName}Service)
    : IPacketHandler<{PacketName}>
{
    public async Task HandleAsync(PlayerState player, {PacketName} packet)
    {
        // 1. Validate player state
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted action without character/map", 
                player.SessionId);
            return;
        }

        // 2. Validate packet data
        if (packet.Amount <= 0)
        {
            logger.LogDebug("Invalid amount {Amount} from {Character}", 
                packet.Amount, player.Character.Name);
            return;
        }

        // 3. Perform game logic
        logger.LogInformation("Player {Character} performing action with {ItemId}", 
            player.Character.Name, packet.ItemId);

        var success = {serviceName}Service.TryDoAction(player.Character, packet.ItemId, packet.Amount);

        if (!success)
        {
            logger.LogDebug("Action failed for {Character}", player.Character.Name);
            return;
        }

        // 4. Send response packet
        await player.Send(new {ResponsePacketName}
        {
            // Response data
        });

        // 5. Optionally broadcast to map
        // await player.CurrentMap.Broadcast(new SomeNotificationPacket { ... });
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, ({PacketName})packet);
    }
}
```

### Common Patterns

#### NPC Interaction
```csharp
// Find NPC on map
var npc = player.CurrentMap.Npcs.FirstOrDefault(n => n.Index == packet.NpcIndex);
if (npc == null)
{
    logger.LogWarning("NPC {Index} not found on map {MapId}", packet.NpcIndex, player.CurrentMap.Id);
    return;
}

// Validate distance
var distance = Math.Abs(player.Character.X - npc.X) + Math.Abs(player.Character.Y - npc.Y);
if (distance > 1)
{
    logger.LogDebug("Player too far from NPC");
    return;
}
```

#### Item Operations
```csharp
// Check inventory
if (!inventoryService.HasItem(player.Character, packet.ItemId, packet.Amount))
{
    return;
}

// Remove from inventory
if (!inventoryService.TryRemoveItem(player.Character, packet.ItemId, packet.Amount))
{
    return;
}
```

#### Map Broadcasting
```csharp
// Notify all players on map
await player.CurrentMap.BroadcastExcept(player.SessionId, new SomeServerPacket
{
    PlayerId = player.SessionId,
    // ...
});
```

### Registration

Packet handlers are auto-registered via `AddPacketHandlers()` in `IocExtensions.cs`. No manual registration needed.

### Related Files

- Packet definitions: `Moffat.EndlessOnline.SDK.Protocol.Net.Client`
- Server packets: `Moffat.EndlessOnline.SDK.Protocol.Net.Server`
- Player state: `src/Acorn/Net/PlayerState.cs`
- Map state: `src/Acorn/World/Map/MapState.cs`
