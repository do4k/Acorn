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
    IStatSkillService statSkillService,
    IDbRepository<Database.Models.Character> characterRepository,
    ICharacterMapper characterMapper)
    : IPacketHandler<StatSkillJunkClientPacket>
{
    public async Task HandleAsync(PlayerState player, StatSkillJunkClientPacket packet)
    {
        if (packet.SessionId != player.SessionId)
        {
            return;
        }

        var npc = NpcInteractionHelper.ValidateInteraction(player, NpcType.Trainer, logger);
        if (npc is null) return;

        var character = player.Character!;

        // Reset base stats/skills and return the points earned for the level.
        statSkillService.Reset(character, dataFileRepository.Ecf);

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
                    Str = character.AdjStr,
                    Intl = character.AdjInt,
                    Wis = character.AdjWis,
                    Agi = character.AdjAgi,
                    Con = character.AdjCon,
                    Cha = character.AdjCha
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
