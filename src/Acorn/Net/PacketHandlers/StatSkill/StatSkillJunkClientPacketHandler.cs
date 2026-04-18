using Acorn.Database.Repository;
using Acorn.Game.Mappers;
using Acorn.Game.Services;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Net.PacketHandlers.StatSkill;

[RequiresCharacter]
public class StatSkillJunkClientPacketHandler(
    ILogger<StatSkillJunkClientPacketHandler> logger,
    IDataFileRepository dataFileRepository,
    IStatCalculator statCalculator,
    IDbRepository<Database.Models.Character> characterRepository,
    ICharacterMapper characterMapper)
    : IPacketHandler<StatSkillJunkClientPacket>
{
    private const int StatPointsPerLevel = 3;
    private const int SkillPointsPerLevel = 3;

    public async Task HandleAsync(PlayerState player, StatSkillJunkClientPacket packet)
    {
        if (packet.SessionId != player.SessionId)
        {
            return;
        }

        var npc = NpcInteractionHelper.ValidateInteraction(player, NpcType.Trainer, logger);
        if (npc is null) return;

        var character = player.Character!;

        // Reset all base stats to 0
        character.Str = 0;
        character.Int = 0;
        character.Wis = 0;
        character.Agi = 0;
        character.Con = 0;
        character.Cha = 0;

        // Remove all spells
        character.Spells = new Game.Models.Spells(new System.Collections.Concurrent.ConcurrentBag<Game.Models.Spell>());

        // Return all stat and skill points
        character.StatPoints = character.Level * StatPointsPerLevel;
        character.SkillPoints = character.Level * SkillPointsPerLevel;

        // Recalculate secondary stats
        statCalculator.RecalculateStats(character, dataFileRepository.Ecf);

        // Save to database
        await characterRepository.UpdateAsync(characterMapper.ToDatabase(character));

        logger.LogInformation("Player {Character} reset their character stats and skills",
            character.Name);

        await player.Send(new StatSkillJunkServerPacket
        {
            Stats = new CharacterStatsReset
            {
                StatPoints = character.StatPoints,
                SkillPoints = character.SkillPoints,
                Hp = character.Hp,
                MaxHp = character.MaxHp,
                Tp = character.Tp,
                MaxTp = character.MaxTp,
                MaxSp = character.MaxSp,
                Base = new CharacterBaseStats
                {
                    Str = character.Str,
                    Intl = character.Int,
                    Wis = character.Wis,
                    Agi = character.Agi,
                    Con = character.Con,
                    Cha = character.Cha
                },
                Secondary = new CharacterSecondaryStats
                {
                    MinDamage = character.MinDamage,
                    MaxDamage = character.MaxDamage,
                    Accuracy = character.Accuracy,
                    Evade = character.Evade,
                    Armor = character.Armor
                }
            }
        });
    }
}
