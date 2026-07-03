using Acorn.World.Services.Spell;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Spell;

[RequiresCharacter]
public class SpellTargetOtherClientPacketHandler(
    ISpellCastService spellCastService,
    ILogger<SpellTargetOtherClientPacketHandler> logger)
    : IPacketHandler<SpellTargetOtherClientPacket>
{
    public async Task HandleAsync(PlayerState player, SpellTargetOtherClientPacket packet)
    {
        // Validate spell_id matches what was requested
        if (player.SpellId != packet.SpellId)
        {
            logger.LogWarning("Player {Character} spell ID mismatch: expected {ExpectedId}, got {ActualId}",
                player.Character!.Name, player.SpellId, packet.SpellId);
            return;
        }

        // Validate timestamp
        if (!spellCastService.ValidateCastTime(player, packet.SpellId, packet.Timestamp))
        {
            logger.LogWarning("Player {Character} spell timestamp validation failed for spell {SpellId}",
                player.Character!.Name, packet.SpellId);
            return;
        }

        logger.LogInformation("Player {Character} casting spell {SpellId} on {TargetType} {VictimId}",
            player.Character!.Name, packet.SpellId, packet.TargetType, packet.VictimId);

        // Update player state
        player.Timestamp = packet.Timestamp;
        player.SpellId = null;

        var target = packet.TargetType switch
        {
            SpellTargetType.Player => SpellCastTarget.Player(packet.VictimId),
            SpellTargetType.Npc => SpellCastTarget.Npc(packet.VictimId),
            _ => (SpellCastTarget?)null
        };

        if (target is null)
        {
            return;
        }

        await spellCastService.CastAsync(player, packet.SpellId, target.Value);
    }

}
