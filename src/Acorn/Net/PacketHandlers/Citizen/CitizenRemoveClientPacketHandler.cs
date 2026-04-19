using Acorn.Data;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Net.PacketHandlers.Citizen;

/// <summary>
///     Handles Citizen_Remove - player gives up citizenship of a town at an inn NPC.
///     Matches reoserv map/inn/remove_citizenship.rs.
/// </summary>
[RequiresCharacter]
internal class CitizenRemoveClientPacketHandler(
    ILogger<CitizenRemoveClientPacketHandler> logger,
    IInnDataRepository innDataRepository)
    : IPacketHandler<CitizenRemoveClientPacket>
{
    public async Task HandleAsync(PlayerState player, CitizenRemoveClientPacket packet)
    {
        var npc = NpcInteractionHelper.ValidateInteraction(player, NpcType.Inn, logger);
        if (npc is null) return;

        var inn = innDataRepository.GetInnByBehaviorId(npc.Data.BehaviorId);
        if (inn is null)
        {
            logger.LogWarning("No inn data found for NPC behavior ID {BehaviorId}", npc.Data.BehaviorId);
            return;
        }

        var defaultHome = innDataRepository.DefaultHomeName;
        var currentHome = player.Character!.Home ?? defaultHome;

        // If player's home is the default or doesn't match this inn, they're not a citizen here
        if (currentHome.Equals(defaultHome, StringComparison.OrdinalIgnoreCase)
            || !currentHome.Equals(inn.Name, StringComparison.OrdinalIgnoreCase))
        {
            await player.Send(new CitizenRemoveServerPacket
            {
                ReplyCode = InnUnsubscribeReply.NotCitizen
            });
            return;
        }

        // Reset home to default
        player.Character!.Home = defaultHome;

        logger.LogInformation("Player {Character} gave up citizenship of {InnName}",
            player.Character!.Name, inn.Name);

        await player.Send(new CitizenRemoveServerPacket
        {
            ReplyCode = InnUnsubscribeReply.Unsubscribed
        });
    }
}
