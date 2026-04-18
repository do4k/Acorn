using Acorn.Database.Repository;
using Acorn.Game.Mappers;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Net.PacketHandlers.StatSkill;

[RequiresCharacter]
public class StatSkillRemoveClientPacketHandler(
    ILogger<StatSkillRemoveClientPacketHandler> logger,
    IDbRepository<Database.Models.Character> characterRepository,
    ICharacterMapper characterMapper)
    : IPacketHandler<StatSkillRemoveClientPacket>
{
    public async Task HandleAsync(PlayerState player, StatSkillRemoveClientPacket packet)
    {
        if (packet.SessionId != player.SessionId)
        {
            return;
        }

        if (player.InteractingNpcIndex is null)
        {
            return;
        }

        var npcIndex = player.InteractingNpcIndex.Value;
        if (!player.CurrentMap.Npcs.TryGetValue(npcIndex, out var npc))
        {
            return;
        }

        if (npc.Data.Type != NpcType.Trainer)
        {
            return;
        }

        var spellId = packet.SpellId;
        if (spellId <= 0)
        {
            return;
        }

        var character = player.Character;

        // Check if player knows this spell
        if (!character.Spells.Items.Any(s => s.Id == spellId))
        {
            return;
        }

        // Remove the spell
        var updatedSpells = character.Spells.Items
            .Where(s => s.Id != spellId)
            .ToList();
        character.Spells = new Domain.Models.Spells(new System.Collections.Concurrent.ConcurrentBag<Domain.Models.Spell>(updatedSpells));

        // Save to database
        await characterRepository.UpdateAsync(characterMapper.ToDatabase(character));

        logger.LogInformation("Player {Character} forgot spell {SpellId}",
            character.Name, spellId);

        await player.Send(new StatSkillRemoveServerPacket
        {
            SpellId = spellId
        });
    }
}
