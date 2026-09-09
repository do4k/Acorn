using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Game.Services;
using Acorn.Net;
using Acorn.Options;
using Acorn.Shared.Caching;
using Acorn.World.Map;
using NpcState = Acorn.World.Npc.NpcState;
using Acorn.World.Services.Party;
using Acorn.World.Services.Player;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using NpcType = Moffat.EndlessOnline.SDK.Protocol.Pub.NpcType;

namespace Acorn.World.Services.Spell;

public class SpellCastService(
    IDataFileRepository dataFiles,
    IFormulaService formulaService,
    IPartyService partyService,
    ILootService lootService,
    IPlayerController playerController,
    ICharacterCacheService characterCache,
    IPaperdollService paperdollService,
    IOptions<ServerOptions> serverOptions,
    ILogger<SpellCastService> logger)
    : ISpellCastService
{
    // MAX_TIMESTAMP and the cast-time window formula match reoserv's
    // player::check_timestamp / utils::timestamp_diff exactly.
    private const int MaxTimestamp = 8640000;
    private readonly int _dropProtectionTicks = serverOptions.Value.DropProtectionTicks;

    public async Task StartChantAsync(PlayerState player, int spellId)
    {
        if (player.Character is null || player.CurrentMap is null)
        {
            return;
        }

        if (!player.Character.Spells.Items.Any(s => s.Id == spellId))
        {
            return;
        }

        if (player.Character.Hidden)
        {
            return;
        }

        await player.CurrentMap.BroadcastPacket(new SpellRequestServerPacket
        {
            PlayerId = player.SessionId,
            SpellId = spellId
        }, player);
    }

    public bool ValidateCastTime(PlayerState player, int spellId, int timestamp)
    {
        var spell = dataFiles.Esf.GetSkill(spellId);
        if (spell is null)
        {
            return false;
        }

        var diff = TimestampDiff(timestamp, player.Timestamp);
        return diff >= (spell.CastTime - 1) * 47 + 35 && diff < Math.Max(spell.CastTime, 1) * 50;
    }

    public async Task CastAsync(PlayerState player, int spellId, SpellCastTarget target)
    {
        if (player.Character is null || player.CurrentMap is null)
        {
            return;
        }

        if (!player.Character.Spells.Items.Any(s => s.Id == spellId))
        {
            return;
        }

        var spell = dataFiles.Esf.GetSkill(spellId);
        if (spell is null)
        {
            return;
        }

        switch (spell.Type)
        {
            case SkillType.Heal:
                await CastHealSpell(player, spellId, spell, target);
                break;
            case SkillType.Attack:
                await CastDamageSpell(player, spellId, spell, target);
                break;
        }
    }

    private async Task CastHealSpell(PlayerState player, int spellId, EsfRecord spell, SpellCastTarget target)
    {
        if (spell.TargetRestrict != SkillTargetRestrict.Friendly)
        {
            return;
        }

        switch (target.Type)
        {
            case SpellCastTargetType.Self:
                await CastHealSelf(player, spellId, spell);
                break;
            case SpellCastTargetType.Group:
                await CastHealGroup(player, spellId, spell);
                break;
            case SpellCastTargetType.OtherPlayer:
                await CastHealOtherPlayer(player, target.VictimId, spellId, spell);
                break;
        }
    }

    private async Task CastHealSelf(PlayerState player, int spellId, EsfRecord spell)
    {
        if (spell.TargetType != SkillTargetType.Self)
        {
            return;
        }

        var character = player.Character!;
        if (character.Tp < spell.TpCost)
        {
            return;
        }

        character.Tp -= spell.TpCost;
        var originalHp = character.Hp;
        character.Hp = Math.Min(character.Hp + spell.HpHeal, character.MaxHp);
        var hpPercentage = HpPercentage(character.Hp, character.MaxHp);

        await player.Send(new SpellTargetSelfServerPacket
        {
            PlayerId = player.SessionId,
            SpellId = spellId,
            SpellHealHp = spell.HpHeal,
            HpPercentage = hpPercentage,
            Hp = character.Hp,
            Tp = character.Tp
        });

        await player.CurrentMap!.BroadcastPacket(new SpellTargetSelfServerPacket
        {
            PlayerId = player.SessionId,
            SpellId = spellId,
            SpellHealHp = spell.HpHeal,
            HpPercentage = hpPercentage,
            Hp = null,
            Tp = null
        }, player);

        if (character.Hp != originalHp)
        {
            await partyService.BroadcastHpUpdate(player);
        }
    }

    private async Task CastHealOtherPlayer(PlayerState player, int targetSessionId, int spellId, EsfRecord spell)
    {
        if (spell.TargetType != SkillTargetType.Normal)
        {
            return;
        }

        if (!player.CurrentMap!.Players.TryGetValue(targetSessionId, out var targetPlayer) ||
            targetPlayer.Character is null)
        {
            return;
        }

        var character = player.Character!;
        if (character.Tp < spell.TpCost)
        {
            return;
        }

        character.Tp -= spell.TpCost;

        var targetCharacter = targetPlayer.Character;
        var originalHp = targetCharacter.Hp;
        targetCharacter.Hp = Math.Min(targetCharacter.Hp + spell.HpHeal, targetCharacter.MaxHp);
        var hpPercentage = HpPercentage(targetCharacter.Hp, targetCharacter.MaxHp);

        await player.CurrentMap.BroadcastPacket(new SpellTargetOtherServerPacket
        {
            VictimId = targetSessionId,
            CasterId = player.SessionId,
            CasterDirection = character.Direction,
            SpellId = spellId,
            SpellHealHp = spell.HpHeal,
            HpPercentage = hpPercentage,
            Hp = null
        }, targetPlayer);

        await targetPlayer.Send(new SpellTargetOtherServerPacket
        {
            VictimId = targetSessionId,
            CasterId = player.SessionId,
            CasterDirection = character.Direction,
            SpellId = spellId,
            SpellHealHp = spell.HpHeal,
            HpPercentage = hpPercentage,
            Hp = targetCharacter.Hp
        });

        await player.Send(new RecoverPlayerServerPacket { Hp = character.Hp, Tp = character.Tp });

        if (targetCharacter.Hp != originalHp)
        {
            await partyService.BroadcastHpUpdate(targetPlayer);
        }
    }

    private async Task CastHealGroup(PlayerState player, int spellId, EsfRecord spell)
    {
        var character = player.Character!;
        if (character.Tp < spell.TpCost)
        {
            return;
        }

        var party = partyService.GetPlayerParty(player.SessionId);
        if (party is null)
        {
            return;
        }

        character.Tp -= spell.TpCost;

        var healedPlayers = new List<GroupHealTargetPlayer>();
        foreach (var memberId in party.Members)
        {
            if (!player.CurrentMap!.Players.TryGetValue(memberId, out var member) || member.Character is null)
            {
                continue;
            }

            var originalHp = member.Character.Hp;
            member.Character.Hp = Math.Min(member.Character.Hp + spell.HpHeal, member.Character.MaxHp);
            var hpPercentage = HpPercentage(member.Character.Hp, member.Character.MaxHp);

            if (member.Character.Hp != originalHp)
            {
                await partyService.BroadcastHpUpdate(member);
            }

            healedPlayers.Add(new GroupHealTargetPlayer
            {
                PlayerId = memberId,
                HpPercentage = hpPercentage,
                Hp = member.Character.Hp
            });
        }

        if (healedPlayers.Count == 0)
        {
            return;
        }

        await player.CurrentMap!.BroadcastPacket(new SpellTargetGroupServerPacket
        {
            SpellId = spellId,
            CasterId = player.SessionId,
            CasterTp = character.Tp,
            SpellHealHp = spell.HpHeal,
            Players = healedPlayers
        });
    }

    private async Task CastDamageSpell(PlayerState player, int spellId, EsfRecord spell, SpellCastTarget target)
    {
        if (spell.TargetRestrict == SkillTargetRestrict.Friendly || spell.TargetType != SkillTargetType.Normal)
        {
            return;
        }

        switch (target.Type)
        {
            case SpellCastTargetType.Npc:
                await CastDamageNpc(player, target.VictimId, spellId, spell);
                break;
            case SpellCastTargetType.OtherPlayer:
                await CastDamagePlayer(player, target.VictimId, spellId, spell);
                break;
        }
    }

    private async Task CastDamageNpc(PlayerState player, int npcIndex, int spellId, EsfRecord spell)
    {
        var map = player.CurrentMap!;
        var character = player.Character!;

        if (!map.Npcs.TryGetValue(npcIndex, out var npc) || npc.IsDead)
        {
            return;
        }

        if (npc.Data.Type is not (NpcType.Aggressive or NpcType.Passive))
        {
            return;
        }

        if (character.Tp < spell.TpCost)
        {
            return;
        }

        character.Tp -= spell.TpCost;

        var damage = formulaService.CalculateDamageToNpc(character, npc.Data, npc.Hp,
            bonusMinDamage: spell.MinDamage, bonusMaxDamage: spell.MaxDamage);
        npc.Hp = Math.Max(0, npc.Hp - damage);

        if (damage > 0)
        {
            npc.AddOpponent(player.SessionId, damage);
        }

        await player.Send(new RecoverPlayerServerPacket { Hp = character.Hp, Tp = character.Tp });

        var hpPercentage = npc.Data.Hp > 0 ? (int)Math.Max((double)npc.Hp / npc.Data.Hp * 100, 0) : 0;

        if (npc.Hp > 0)
        {
            await player.Send(new CastReplyServerPacket
            {
                SpellId = spellId,
                CasterId = player.SessionId,
                CasterDirection = character.Direction,
                NpcIndex = npcIndex,
                Damage = damage,
                HpPercentage = hpPercentage,
                CasterTp = character.Tp,
                KillStealProtection = NpcKillStealProtectionState.Unprotected
            });

            await map.BroadcastPacket(new CastReplyServerPacket
            {
                SpellId = spellId,
                CasterId = player.SessionId,
                CasterDirection = character.Direction,
                NpcIndex = npcIndex,
                Damage = damage,
                HpPercentage = hpPercentage
            }, player);
        }
        else
        {
            await HandleNpcKilledBySpell(player, npc, npcIndex, spellId, damage);
        }
    }

    private async Task HandleNpcKilledBySpell(PlayerState player, NpcState npc, int npcIndex, int spellId,
        int damage)
    {
        var character = player.Character!;
        var map = player.CurrentMap!;

        npc.IsDead = true;
        npc.DeathTime = DateTime.UtcNow;
        npc.Opponents.Clear();

        var experienceGained = npc.Data.Experience;
        character.GainExperience(experienceGained);

        logger.LogInformation("Player {Character} killed NPC {NpcName} with spell {SpellId}",
            character.Name, npc.Data.Name, spellId);

        var levelsGained = 0;
        while (formulaService.CanLevelUp(character))
        {
            formulaService.LevelUp(character, dataFiles.Ecf);
            levelsGained++;
        }

        if (levelsGained > 0)
        {
            await player.CacheCharacterStateAsync(characterCache, paperdollService);
        }

        var dropItem = lootService.RollDrop(npc.Id);
        var dropId = 0;
        var dropAmount = 0;
        var dropIndex = 0;

        if (dropItem != null)
        {
            dropAmount = lootService.RollDropAmount(dropItem);
            dropId = dropItem.ItemId;

            var itemIndex = map.GetNextItemIndex();
            map.Items.TryAdd(itemIndex, new MapItem
            {
                Id = dropId,
                Amount = dropAmount,
                Coords = new Coords { X = npc.X, Y = npc.Y },
                OwnerId = player.SessionId,
                ProtectedTicks = _dropProtectionTicks
            });
            dropIndex = itemIndex;
        }

        var npcKilledData = new NpcKilledData
        {
            KillerId = player.SessionId,
            KillerDirection = character.Direction,
            NpcIndex = npcIndex,
            DropIndex = dropIndex,
            DropId = dropId,
            DropAmount = dropAmount,
            DropCoords = new Coords { X = npc.X, Y = npc.Y },
            Damage = damage
        };

        if (levelsGained > 0)
        {
            await player.Send(new CastAcceptServerPacket
            {
                SpellId = spellId,
                NpcKilledData = npcKilledData,
                CasterTp = character.Tp,
                Experience = character.Exp,
                LevelUp = new LevelUpStats
                {
                    Level = character.Level,
                    StatPoints = character.StatPoints,
                    SkillPoints = character.SkillPoints,
                    MaxHp = character.MaxHp,
                    MaxTp = character.MaxTp,
                    MaxSp = character.MaxSp
                }
            });

            await map.BroadcastPacket(new CastSpecServerPacket
            {
                SpellId = spellId,
                NpcKilledData = npcKilledData
            }, player);
        }
        else
        {
            await player.Send(new CastSpecServerPacket
            {
                SpellId = spellId,
                NpcKilledData = npcKilledData,
                CasterTp = character.Tp,
                Experience = character.Exp
            });

            await map.BroadcastPacket(new CastSpecServerPacket
            {
                SpellId = spellId,
                NpcKilledData = npcKilledData
            }, player);
        }
    }

    private async Task CastDamagePlayer(PlayerState player, int targetSessionId, int spellId, EsfRecord spell)
    {
        var map = player.CurrentMap!;
        var character = player.Character!;

        if (map.Data.Type != Moffat.EndlessOnline.SDK.Protocol.Map.MapType.Pk)
        {
            return;
        }

        if (!map.Players.TryGetValue(targetSessionId, out var targetPlayer) || targetPlayer.Character is null)
        {
            return;
        }

        if (targetPlayer.Character.Hidden)
        {
            return;
        }

        var party = partyService.GetPlayerParty(player.SessionId);
        if (party is not null && party.Members.Contains(targetSessionId))
        {
            return;
        }

        if (character.Tp < spell.TpCost)
        {
            return;
        }

        var damage = formulaService.CalculateDamageToPlayer(character, targetPlayer.Character,
            bonusMinDamage: spell.MinDamage, bonusMaxDamage: spell.MaxDamage);
        targetPlayer.Character.Hp = Math.Max(0, targetPlayer.Character.Hp - damage);
        character.Tp -= spell.TpCost;

        await player.Send(new RecoverPlayerServerPacket { Hp = character.Hp, Tp = character.Tp });

        var dead = targetPlayer.Character.Hp == 0;
        var hpPercentage = HpPercentage(targetPlayer.Character.Hp, targetPlayer.Character.MaxHp);

        await map.BroadcastPacket(new AvatarAdminServerPacket
        {
            CasterId = player.SessionId,
            VictimId = targetSessionId,
            CasterDirection = character.Direction,
            Damage = damage,
            HpPercentage = hpPercentage,
            VictimDied = dead,
            SpellId = spellId
        });

        if (dead)
        {
            await playerController.DieAsync(targetPlayer);
        }

        await targetPlayer.Send(new RecoverPlayerServerPacket
        {
            Hp = targetPlayer.Character.Hp,
            Tp = targetPlayer.Character.Tp
        });

        await partyService.BroadcastHpUpdate(targetPlayer);
    }

    private static int HpPercentage(int hp, int maxHp)
    {
        return maxHp > 0 ? (int)Math.Round(hp * 100.0 / maxHp) : 0;
    }

    private static int TimestampDiff(int a, int b)
    {
        if (a == -1)
        {
            return b;
        }

        if (b == -1)
        {
            return a;
        }

        return b > a ? a - b + MaxTimestamp : a - b;
    }
}
