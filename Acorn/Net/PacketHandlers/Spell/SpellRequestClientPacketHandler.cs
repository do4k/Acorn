using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Spell;

public class SpellRequestClientPacketHandler(ILogger<SpellRequestClientPacketHandler> logger, IWorldQueries worldQueries)
    : IPacketHandler<SpellRequestClientPacket>
{
    public async Task HandleAsync(PlayerState player, SpellRequestClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to cast spell without character or map", player.SessionId);
            return;
        }

        logger.LogInformation("Player {Character} casting spell {SpellId} at timestamp {Timestamp}",
            player.Character.Name, packet.SpellId, packet.Timestamp);

        // TODO: Implement map.CastSpell(player, spellId, timestamp)
        await Task.CompletedTask;
    }

    public Task HandleAsync(PlayerState playerState, Moffat.EndlessOnline.SDK.Protocol.Net.IPacket packet)
    {
        return HandleAsync(playerState, (SpellRequestClientPacket)packet);
    }
}
