using Acorn.Database.Repository;
using Acorn.Game.Services;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Net.PacketHandlers.Bank;

public class BankAddClientPacketHandler(
    ILogger<BankAddClientPacketHandler> logger,
    IDataFileRepository dataFileRepository,
    IInventoryService inventoryService)
    : IPacketHandler<BankAddClientPacket>
{
    private const int GoldItemId = 1;
    private const int MaxBankGold = 2000000000; // ~2 billion cap

    public async Task HandleAsync(PlayerState player, BankAddClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to deposit without character or map", player.SessionId);
            return;
        }

        var requestedAmount = packet.Amount;

        if (requestedAmount <= 0)
        {
            logger.LogWarning("Player {Character} attempted to deposit invalid amount {Amount}",
                player.Character.Name, requestedAmount);
            return;
        }

        // Verify player is interacting with a bank NPC
        if (player.InteractingNpcIndex == null)
        {
            logger.LogWarning("Player {Character} attempted to deposit without interacting with NPC",
                player.Character.Name);
            return;
        }

        var npcIndex = player.InteractingNpcIndex.Value;
        var npc = player.CurrentMap.Npcs
            .Select((n, i) => (npc: n, index: i))
            .FirstOrDefault(x => x.index == npcIndex);

        if (npc.npc == null || npc.npc.Data.Type != NpcType.Bank)
        {
            logger.LogWarning("Player {Character} tried to deposit at invalid bank NPC",
                player.Character.Name);
            return;
        }

        // Get player's gold amount
        var playerGold = inventoryService.GetItemAmount(player.Character, GoldItemId);
        var amount = Math.Min(requestedAmount, playerGold);

        if (amount <= 0)
        {
            return;
        }

        // Check bank gold cap
        var availableBankSpace = MaxBankGold - player.Character.GoldBank;
        amount = Math.Min(amount, availableBankSpace);

        if (amount <= 0)
        {
            logger.LogDebug("Player {Character}'s bank is full", player.Character.Name);
            return;
        }

        // Remove gold from inventory
        if (!inventoryService.TryRemoveItem(player.Character, GoldItemId, amount))
        {
            logger.LogWarning("Failed to remove gold from player {Character}", player.Character.Name);
            return;
        }

        // Add gold to bank
        player.Character.GoldBank += amount;

        logger.LogInformation("Player {Character} deposited {Amount} gold (Bank: {BankGold})",
            player.Character.Name, amount, player.Character.GoldBank);

        await player.Send(new BankReplyServerPacket
        {
            GoldInventory = inventoryService.GetItemAmount(player.Character, GoldItemId),
            GoldBank = player.Character.GoldBank
        });
    }

}
