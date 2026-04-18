using Acorn.Game.Services;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Bank;

[RequiresCharacter]
public class BankAddClientPacketHandler(
    ILogger<BankAddClientPacketHandler> logger,
    IInventoryService inventoryService)
    : IPacketHandler<BankAddClientPacket>
{
    private const int GoldItemId = 1;
    private const int MaxBankGold = 2000000000; // ~2 billion cap

    public async Task HandleAsync(PlayerState player, BankAddClientPacket packet)
    {
        var requestedAmount = packet.Amount;

        if (requestedAmount <= 0)
        {
            logger.LogWarning("Player {Character} attempted to deposit invalid amount {Amount}",
                player.Character!.Name, requestedAmount);
            return;
        }

        // Verify player is interacting with a bank NPC
        var npc = NpcInteractionHelper.ValidateInteraction(player, NpcType.Bank, logger);
        if (npc is null) return;

        // Get player's gold amount
        var playerGold = inventoryService.GetItemAmount(player.Character!, GoldItemId);
        var amount = Math.Min(requestedAmount, playerGold);

        if (amount <= 0)
        {
            return;
        }

        // Check bank gold cap
        var availableBankSpace = MaxBankGold - player.Character!.GoldBank;
        amount = Math.Min(amount, availableBankSpace);

        if (amount <= 0)
        {
            logger.LogDebug("Player {Character}'s bank is full", player.Character!.Name);
            return;
        }

        // Remove gold from inventory
        if (!inventoryService.TryRemoveItem(player.Character!, GoldItemId, amount))
        {
            logger.LogWarning("Failed to remove gold from player {Character}", player.Character!.Name);
            return;
        }

        // Add gold to bank
        player.Character!.GoldBank += amount;

        logger.LogInformation("Player {Character} deposited {Amount} gold (Bank: {BankGold})",
            player.Character!.Name, amount, player.Character!.GoldBank);

        await player.Send(new BankReplyServerPacket
        {
            GoldInventory = inventoryService.GetItemAmount(player.Character!, GoldItemId),
            GoldBank = player.Character!.GoldBank
        });
    }

}
