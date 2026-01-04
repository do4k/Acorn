using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.StatSkill;

public class StatSkillAddClientPacketHandler(
    ILogger<StatSkillAddClientPacketHandler> logger,
    IWorldQueries worldQueries)
    : IPacketHandler<StatSkillAddClientPacket>
{
    public async Task HandleAsync(PlayerState player, StatSkillAddClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to add stat without character or map", player.SessionId);
            return;
        }

        logger.LogInformation("Player {Character} adding stat point to {ActionType}",
            player.Character.Name, packet.ActionType);

        // TODO: Implement player.AddStat(actionType)
        await Task.CompletedTask;
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (StatSkillAddClientPacket)packet);
    }
}