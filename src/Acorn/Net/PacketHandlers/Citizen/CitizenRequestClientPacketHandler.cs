using Acorn.Data;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Citizen;

[RequiresCharacter]
public class CitizenRequestClientPacketHandler(
    ILogger<CitizenRequestClientPacketHandler> logger,
    IInnDataRepository innDataRepository)
    : IPacketHandler<CitizenRequestClientPacket>
{
    public async Task HandleAsync(PlayerState player, CitizenRequestClientPacket packet)
    {
        var npc = NpcInteractionHelper.ValidateInteraction(player, NpcType.Inn, logger);
        if (npc is null) return;

        // Get inn data by NPC's behavior ID
        var inn = innDataRepository.GetInnByBehaviorId(npc.Data.BehaviorId);
        if (inn == null)
        {
            logger.LogWarning("No inn data found for NPC behavior ID {BehaviorId}", npc.Data.BehaviorId);
            return;
        }

        // Check if player is already at full HP and TP
        if (player.Character.Hp >= player.Character.MaxHp && player.Character.Tp >= player.Character.MaxTp)
        {
            logger.LogInformation("Player {Character} already at full HP/TP, sleep not needed",
                player.Character.Name);
            return;
        }

        // Check if this is the player's home inn
        var currentHome = player.Character.Home ?? innDataRepository.DefaultHomeName;
        if (!inn.Name.Equals(currentHome, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Player {Character} tried to sleep at inn {InnName} but home is {Home}",
                player.Character.Name, inn.Name, currentHome);
            return;
        }

        // Calculate sleep cost (HP to restore + TP to restore)
        var hpToRestore = player.Character.MaxHp - player.Character.Hp;
        var tpToRestore = player.Character.MaxTp - player.Character.Tp;
        var cost = hpToRestore + tpToRestore;

        // Store the cost for when they confirm
        player.SleepCost = cost;

        logger.LogInformation("Player {Character} requesting sleep at {InnName}, cost: {Cost}",
            player.Character.Name, inn.Name, cost);

        await player.Send(new CitizenRequestServerPacket
        {
            Cost = cost
        });
    }

}
