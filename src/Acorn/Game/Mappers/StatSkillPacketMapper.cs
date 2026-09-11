using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Character = Acorn.Game.Models.Character;
using NetSpell = Moffat.EndlessOnline.SDK.Protocol.Net.Spell;

namespace Acorn.Game.Mappers;

/// <summary>
///     Builds the StatSkill server packets the vanilla client expects.
///     Stat point spends use StatSkill/Player; skill point spends use
///     StatSkill/Accept (skill points + spell id + new level).
/// </summary>
public static class StatSkillPacketMapper
{
    public static StatSkillPlayerServerPacket ToStatPlayer(Character character)
    {
        return new StatSkillPlayerServerPacket
        {
            StatPoints = character.StatPoints,
            Stats = new CharacterStatsUpdate
            {
                MaxHp = character.MaxHp,
                MaxTp = character.MaxTp,
                MaxSp = character.MaxSp,
                BaseStats = new CharacterBaseStats
                {
                    Str = character.AdjStr,
                    Intl = character.AdjInt,
                    Wis = character.AdjWis,
                    Agi = character.AdjAgi,
                    Con = character.AdjCon,
                    Cha = character.AdjCha
                },
                SecondaryStats = new CharacterSecondaryStats
                {
                    MinDamage = character.MinDamage,
                    MaxDamage = character.MaxDamage,
                    Accuracy = character.Accuracy,
                    Evade = character.Evade,
                    Armor = character.Armor
                }
            }
        };
    }

    public static StatSkillAcceptServerPacket ToSkillAccept(int skillPoints, int spellId, int level)
    {
        return new StatSkillAcceptServerPacket
        {
            SkillPoints = skillPoints,
            Spell = new NetSpell
            {
                Id = spellId,
                Level = level
            }
        };
    }

    public static StatSkillReplyServerPacket ToWrongClassReply(int classId)
    {
        return new StatSkillReplyServerPacket
        {
            ReplyCode = SkillMasterReply.WrongClass,
            ReplyCodeData = new StatSkillReplyServerPacket.ReplyCodeDataWrongClass
            {
                ClassId = classId
            }
        };
    }
}
