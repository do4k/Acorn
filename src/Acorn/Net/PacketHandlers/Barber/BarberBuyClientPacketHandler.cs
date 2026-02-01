using Acorn.Database.Repository;
using Acorn.Game.Services;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Net.PacketHandlers.Barber;

public class BarberBuyClientPacketHandler(
    ILogger<BarberBuyClientPacketHandler> logger,
    IDataFileRepository dataFileRepository,
    IInventoryService inventoryService)
    : IPacketHandler<BarberBuyClientPacket>
{
    private const int GoldItemId = 1;
    private const int MaxHairStyle = 20;
    private const int MaxHairColor = 9;
    private const int BaseCost = 100;
    private const int CostPerLevel = 10;

    public async Task HandleAsync(PlayerState player, BarberBuyClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to buy haircut without character or map", player.SessionId);
            return;
        }

        var hairStyle = packet.HairStyle;
        var hairColor = packet.HairColor;

        // Validate hair style and color ranges
        if (hairStyle < 0 || hairStyle > MaxHairStyle || hairColor < 0 || hairColor > MaxHairColor)
        {
            logger.LogWarning("Player {Character} tried to buy invalid hairstyle {Style}/{Color}",
                player.Character.Name, hairStyle, hairColor);
            return;
        }

        // Verify player is interacting with a barber NPC
        if (player.InteractingNpcIndex == null)
        {
            logger.LogWarning("Player {Character} attempted to buy haircut without interacting with NPC",
                player.Character.Name);
            return;
        }

        var npcIndex = player.InteractingNpcIndex.Value;
        var npc = player.CurrentMap.Npcs
            .Select((n, i) => (npc: n, index: i))
            .FirstOrDefault(x => x.index == npcIndex);

        if (npc.npc == null || npc.npc.Data.Type != NpcType.Barber)
        {
            logger.LogWarning("Player {Character} tried to buy haircut from invalid barber NPC",
                player.Character.Name);
            return;
        }

        // Calculate cost based on level
        var cost = BaseCost + Math.Max(1, player.Character.Level) * CostPerLevel;

        // Check if player has enough gold
        var playerGold = inventoryService.GetItemAmount(player.Character, GoldItemId);
        if (playerGold < cost)
        {
            logger.LogDebug("Player {Character} doesn't have enough gold ({Gold}) for haircut ({Cost})",
                player.Character.Name, playerGold, cost);
            return;
        }

        // Remove gold
        if (!inventoryService.TryRemoveItem(player.Character, GoldItemId, cost))
        {
            return;
        }

        // Update character appearance
        player.Character.HairStyle = hairStyle;
        player.Character.HairColor = hairColor;

        logger.LogInformation("Player {Character} bought haircut style={Style} color={Color} for {Cost} gold",
            player.Character.Name, hairStyle, hairColor, cost);

        // Create avatar change info
        var avatarChange = new AvatarChange
        {
            PlayerId = player.SessionId,
            Sound = false,
            ChangeType = AvatarChangeType.Hair,
            ChangeTypeData = new AvatarChange.ChangeTypeDataHair
            {
                HairStyle = hairStyle,
                HairColor = hairColor
            }
        };

        // Send response to player
        await player.Send(new BarberAgreeServerPacket
        {
            GoldAmount = inventoryService.GetItemAmount(player.Character, GoldItemId),
            Change = avatarChange
        });

        // Notify other players on the map
        var avatarPacket = new AvatarAgreeServerPacket { Change = avatarChange };
        await player.CurrentMap.BroadcastPacket(avatarPacket, player);
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (BarberBuyClientPacket)packet);
    }
}
