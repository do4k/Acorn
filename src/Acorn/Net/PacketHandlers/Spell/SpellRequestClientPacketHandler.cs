using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Spell;

public class SpellRequestClientPacketHandler(
    ILogger<SpellRequestClientPacketHandler> logger)
    : IPacketHandler<SpellRequestClientPacket>
{
    public async Task HandleAsync(PlayerState player, SpellRequestClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to cast spell without character or map", player.SessionId);
            return;
        }

        if (packet.SpellId <= 0)
        {
            logger.LogWarning("Player {Character} attempted to cast invalid spell {SpellId}",
                player.Character.Name, packet.SpellId);
            return;
        }

        logger.LogInformation("Player {Character} starting spell chant for spell {SpellId} at timestamp {Timestamp}",
            player.Character.Name, packet.SpellId, packet.Timestamp);

        // Update player state for tracking
        player.Timestamp = packet.Timestamp;
        player.SpellId = packet.SpellId;

        // TODO: Implement map.StartSpellChant(player.Id, spellId)
        await Task.CompletedTask;
    }

}