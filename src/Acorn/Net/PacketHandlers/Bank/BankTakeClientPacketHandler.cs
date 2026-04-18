using Acorn.Database.Repository;
using Acorn.Game.Services;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Bank;

[RequiresCharacter]
public class BankTakeClientPacketHandler(
    ILogger<BankTakeClientPacketHandler> logger,
    IDataFileRepository dataFileRepository,
    IInventoryService inventoryService)
    : IPacketHandler<BankTakeClientPacket>
{
    private const int GoldItemId = 1;

    public async Task HandleAsync(PlayerState player, BankTakeClientPacket packet)
    {
        var requestedAmount = packet.Amount;

        if (requestedAmount <= 0)
        {
            logger.LogWarning("Player {Character} attempted to withdraw invalid amount {Amount}",
                player.Character!.Name, requestedAmount);
            return;
        }

        // Verify player is interacting with a bank NPC
        var npc = NpcInteractionHelper.ValidateInteraction(player, NpcType.Bank, logger);
        if (npc is null) return;

        // Get bank gold amount
        var amount = Math.Min(requestedAmount, player.Character!.GoldBank);

        if (amount <= 0)
        {
            return;
        }

        // Check weight limit for gold
        var goldData = dataFileRepository.Eif.GetItem(GoldItemId);
        if (goldData != null && goldData.Weight > 0)
        {
            var currentWeight = CalculateCurrentWeight(player);
            var availableWeight = player.Character!.MaxWeight - currentWeight;
            var canHold = availableWeight / goldData.Weight;
            amount = Math.Min(amount, canHold);
        }

        if (amount <= 0)
        {
            logger.LogDebug("Player {Character} cannot hold any more gold (weight limit)", player.Character!.Name);
            return;
        }

        // Remove gold from bank
        player.Character!.GoldBank -= amount;

        // Add gold to inventory
        inventoryService.TryAddItem(player.Character!, GoldItemId, amount);

        logger.LogInformation("Player {Character} withdrew {Amount} gold (Bank: {BankGold})",
            player.Character!.Name, amount, player.Character!.GoldBank);

        await player.Send(new BankReplyServerPacket
        {
            GoldInventory = inventoryService.GetItemAmount(player.Character!, GoldItemId),
            GoldBank = player.Character!.GoldBank
        });
    }

    private int CalculateCurrentWeight(PlayerState player)
    {
        if (player.Character == null) return 0;

        var totalWeight = 0;
        foreach (var item in player.Character!.Inventory.Items)
        {
            var itemData = dataFileRepository.Eif.GetItem(item.Id);
            if (itemData != null)
            {
                totalWeight += itemData.Weight * item.Amount;
            }
        }

        return totalWeight;
    }

}
