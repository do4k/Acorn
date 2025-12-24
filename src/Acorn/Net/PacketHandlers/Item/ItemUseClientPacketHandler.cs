using Acorn.Database.Repository;
using Acorn.Game.Services;
using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Net.PacketHandlers.Item;

public class ItemUseClientPacketHandler(
    ILogger<ItemUseClientPacketHandler> logger,
    IWorldQueries worldQueries,
    IInventoryService inventoryService)
    : IPacketHandler<ItemUseClientPacket>
{
    public async Task HandleAsync(PlayerState player, ItemUseClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to use item without character or map", player.SessionId);
            return;
        }

        // Validate player has the item
        if (!inventoryService.HasItem(player.Character, packet.ItemId))
        {
            logger.LogWarning("Player {Character} tried to use item {ItemId} but doesn't have it",
                player.Character.Name, packet.ItemId);
            return;
        }

        // Get item data
        var itemData = worldQueries.DataRepository.Eif.GetItem(packet.ItemId);
        if (itemData == null)
        {
            logger.LogError("Item {ItemId} not found in EIF", packet.ItemId);
            return;
        }

        logger.LogInformation("Player {Character} using item {ItemId} ({ItemName}) type {Type}",
            player.Character.Name, packet.ItemId, itemData.Name, itemData.Type);

        bool consumed = true;

        switch (itemData.Type)
        {
            case ItemType.Heal:
                await HandleHealItem(player, itemData);
                break;

            case ItemType.Teleport:
                await HandleTeleportItem(player, itemData);
                break;

            case ItemType.HairDye:
                await HandleHairDye(player, itemData);
                break;

            case ItemType.ExpReward:
                await HandleExpReward(player, itemData);
                break;

            default:
                logger.LogWarning("Item type {Type} not yet implemented", itemData.Type);
                consumed = false;
                break;
        }

        // Remove item from inventory if consumed
        if (consumed)
        {
            inventoryService.TryRemoveItem(player.Character, packet.ItemId, 1);
            // TODO: Send updated inventory packet
        }

        await Task.CompletedTask;
    }

    private async Task HandleHealItem(PlayerState player, EifRecord item)
    {
        if (player.Character == null) return;

        int hpBefore = player.Character.Hp;
        int tpBefore = player.Character.Tp;

        // Heal HP
        if (item.Hp > 0)
        {
            player.Character.Hp = Math.Min(player.Character.Hp + item.Hp, player.Character.MaxHp);
        }

        // Heal TP
        if (item.Tp > 0)
        {
            player.Character.Tp = Math.Min(player.Character.Tp + item.Tp, player.Character.MaxTp);
        }

        int hpGain = player.Character.Hp - hpBefore;
        int tpGain = player.Character.Tp - tpBefore;

        logger.LogInformation("Player {Character} healed {HpGain} HP and {TpGain} TP",
            player.Character.Name, hpGain, tpGain);

        // TODO: Broadcast RecoverAgree packet to nearby players
        // if (hpGain > 0 || tpGain > 0) { await player.CurrentMap.BroadcastPacket(...); }
    }

    private async Task HandleTeleportItem(PlayerState player, EifRecord item)
    {
        if (player.Character == null) return;

        // Check if map allows scrolling
        // TODO: Add CanScroll property to map data
        // if (!player.CurrentMap.Data.CanScroll) return;

        int targetMapId;
        int targetX, targetY;

        if (item.Spec1 == 0)
        {
            // Teleport to home (inn)
            // TODO: Get home coordinates from INN database
            targetMapId = 1; // Default home map
            targetX = 12;
            targetY = 6;
            logger.LogInformation("Player {Character} using scroll to teleport home", player.Character.Name);
        }
        else
        {
            // Teleport to specific map/coordinates
            targetMapId = item.Spec1;
            targetX = item.Spec2;
            targetY = item.Spec3;
            logger.LogInformation("Player {Character} using scroll to teleport to map {MapId} ({X}, {Y})",
                player.Character.Name, targetMapId, targetX, targetY);
        }

        // TODO: Implement warp with scroll effect
        // await worldQueries.WarpPlayer(player, targetMapId, targetX, targetY, WarpEffect.Scroll);

        await Task.CompletedTask;
    }

    private async Task HandleHairDye(PlayerState player, EifRecord item)
    {
        if (player.Character == null) return;

        player.Character.HairColor = item.Spec1;

        logger.LogInformation("Player {Character} changed hair color to {Color}",
            player.Character.Name, item.Spec1);

        // TODO: Broadcast AvatarAgree packet to nearby players
        // await player.CurrentMap.BroadcastPacket(new AvatarAgreeServerPacket { ... });
    }

    private async Task HandleExpReward(PlayerState player, EifRecord item)
    {
        if (player.Character == null) return;

        int expGain = item.Spec1;
        player.Character.Exp += expGain;

        logger.LogInformation("Player {Character} gained {Exp} experience from item",
            player.Character.Name, expGain);

        // TODO: Check for level up
        // TODO: Send experience update packet

        await Task.CompletedTask;
    }

    public Task HandleAsync(PlayerState playerState, Moffat.EndlessOnline.SDK.Protocol.Net.IPacket packet)
    {
        return HandleAsync(playerState, (ItemUseClientPacket)packet);
    }
}
