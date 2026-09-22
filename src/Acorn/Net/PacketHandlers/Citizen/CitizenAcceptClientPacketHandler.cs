using Acorn.Data;
using Acorn.Game.Services;
using Acorn.World;
using Acorn.World.Services.Player;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Citizen;

[RequiresCharacter]
public class CitizenAcceptClientPacketHandler(
    ILogger<CitizenAcceptClientPacketHandler> logger,
    IInnDataRepository innDataRepository,
    IInventoryService inventoryService,
    IWorldQueries worldQueries,
    IPlayerController playerController)
    : IPacketHandler<CitizenAcceptClientPacket>
{
    private const int GoldItemId = 1;

    public async Task HandleAsync(PlayerState player, CitizenAcceptClientPacket packet)
    {
        // Check if there's a pending sleep cost
        var cost = player.SleepCost;
        if (cost == null || cost <= 0)
        {
            logger.LogWarning("Player {Character} attempted sleep accept without pending cost",
                player.Character!.Name);
            return;
        }

        var npc = NpcInteractionHelper.ValidateInteraction(player, NpcType.Inn, logger);
        if (npc is null) return;

        // Get inn data by NPC's behavior ID
        var inn = innDataRepository.GetInnByBehaviorId(npc.Data.BehaviorId);
        if (inn == null)
        {
            logger.LogWarning("No inn data found for NPC behavior ID {BehaviorId}", npc.Data.BehaviorId);
            return;
        }

        // Check if this is the player's home inn
        var currentHome = player.Character!.Home ?? innDataRepository.DefaultHomeName;
        if (!inn.Name.Equals(currentHome, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Player {Character} tried to sleep at inn {InnName} but home is {Home}",
                player.Character!.Name, inn.Name, currentHome);
            return;
        }

        // Check if player has enough gold
        var goldAmount = inventoryService.GetItemAmount(player.Character!, GoldItemId);
        if (goldAmount < cost.Value)
        {
            logger.LogWarning("Player {Character} doesn't have enough gold for sleep (has {Gold}, needs {Cost})",
                player.Character!.Name, goldAmount, cost.Value);
            player.SleepCost = null;
            return;
        }

        // Remove gold
        if (!inventoryService.TryRemoveItem(player.Character!, GoldItemId, cost.Value))
        {
            logger.LogWarning("Player {Character} failed to remove gold for sleep",
                player.Character!.Name);
            player.SleepCost = null;
            return;
        }

        // Restore HP and TP
        player.Character!.Hp = player.Character!.MaxHp;
        player.Character!.Tp = player.Character!.MaxTp;

        // Clear sleep cost
        player.SleepCost = null;
        player.InteractingNpcIndex = null;

        logger.LogInformation("Player {Character} slept at {InnName} for {Cost} gold",
            player.Character!.Name, inn.Name, cost.Value);

        // Get remaining gold
        var remainingGold = inventoryService.GetItemAmount(player.Character!, GoldItemId);

        await player.Send(new CitizenAcceptServerPacket
        {
            GoldAmount = remainingGold
        });

        // Warp the player to the inn's sleeping area, mirroring eoserv's Citizen/Accept
        // (random warp effect). Falls back to staying put if no sleep location is configured.
        if (inn.SleepMap > 0)
        {
            var sleepMap = worldQueries.FindMap(inn.SleepMap);
            if (sleepMap is null)
            {
                logger.LogWarning("Sleep map {SleepMapId} for inn {InnName} not found, skipping warp for {Character}",
                    inn.SleepMap, inn.Name, player.Character!.Name);
            }
            else
            {
                await playerController.WarpAsync(player, sleepMap, inn.SleepX, inn.SleepY, WarpEffect.Scroll);
            }
        }
    }

}
