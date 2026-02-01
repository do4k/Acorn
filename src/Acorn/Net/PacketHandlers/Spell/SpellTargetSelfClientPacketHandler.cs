using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Spell;

public class SpellTargetSelfClientPacketHandler(
    ILogger<SpellTargetSelfClientPacketHandler> logger)
    : IPacketHandler<SpellTargetSelfClientPacket>
{
    public async Task HandleAsync(PlayerState player, SpellTargetSelfClientPacket packet)
    {
        if (player.Character == null || player.CurrentMap == null)
        {
            logger.LogWarning("Player {SessionId} attempted to cast spell without character or map", player.SessionId);
            return;
        }

        // Validate spell_id matches what was requested
        if (player.SpellId != packet.SpellId)
        {
            logger.LogWarning("Player {Character} spell ID mismatch: expected {ExpectedId}, got {ActualId}",
                player.Character.Name, player.SpellId, packet.SpellId);
            return;
        }

        // Validate timestamp
        if (!CheckTimestamp(player, packet.SpellId, packet.Timestamp))
        {
            logger.LogWarning("Player {Character} spell timestamp validation failed for spell {SpellId}",
                player.Character.Name, packet.SpellId);
            return;
        }

        logger.LogInformation("Player {Character} casting spell {SpellId} on self",
            player.Character.Name, packet.SpellId);

        // Update player state
        player.Timestamp = packet.Timestamp;
        player.SpellId = null;

        // TODO: Implement map.CastSpell(player, spellId, SpellTarget.Player)
        await Task.CompletedTask;
    }


    private bool CheckTimestamp(PlayerState player, int spellId, int timestamp)
    {
        // TODO: Load spell data from database and validate cast time
        // For now, just do basic validation that timestamp has progressed
        return timestamp >= player.Timestamp;
    }
}
