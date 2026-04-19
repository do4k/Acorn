using Acorn.Game.Services;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Net.PacketHandlers.Locker;

/// <summary>
///     Handles Locker_Buy - player upgrades locker capacity at a banker NPC.
///     Matches reoserv map/bank/upgrade_locker.rs.
///     Cost formula: BaseCost + CostStep * currentUpgradeLevel.
/// </summary>
[RequiresCharacter]
internal class LockerBuyClientPacketHandler(
    ILogger<LockerBuyClientPacketHandler> logger,
    IInventoryService inventoryService)
    : IPacketHandler<LockerBuyClientPacket>
{
    private const int GoldItemId = 1;
    private const int MaxUpgrades = 7;
    private const int UpgradeBaseCost = 1000;
    private const int UpgradeCostStep = 1000;

    public async Task HandleAsync(PlayerState player, LockerBuyClientPacket packet)
    {
        var npc = NpcInteractionHelper.ValidateInteraction(player, NpcType.Bank, logger);
        if (npc is null) return;

        var character = player.Character!;

        // Check max upgrades
        if (character.BankMax >= MaxUpgrades)
        {
            return;
        }

        // Calculate cost for next upgrade
        var cost = UpgradeBaseCost + UpgradeCostStep * character.BankMax;

        // Check gold
        var goldAmount = inventoryService.GetItemAmount(character, GoldItemId);
        if (goldAmount < cost)
        {
            return;
        }

        // Deduct gold and increment upgrade level
        if (!inventoryService.TryRemoveItem(character, GoldItemId, cost))
        {
            return;
        }

        character.BankMax++;

        logger.LogInformation("Player {Character} upgraded locker to level {Level} for {Cost} gold",
            character.Name, character.BankMax, cost);

        await player.Send(new LockerBuyServerPacket
        {
            GoldAmount = inventoryService.GetItemAmount(character, GoldItemId),
            LockerUpgrades = character.BankMax
        });
    }
}
