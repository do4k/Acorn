using Acorn.Database.Repository;
using Acorn.Shared.Caching;
using Acorn.Shared.Models.Pub;
using Microsoft.Extensions.Logging;

namespace Acorn.Infrastructure;

/// <summary>
///     Reloads the pub data files and repopulates the pub cache. Used at startup
///     by <see cref="PubFileCacheHostedService" /> and at runtime by the
///     <c>$repub</c> admin command.
/// </summary>
public class PubFileReloadService(
    IDataFileRepository dataFiles,
    IPubCacheService pubCache,
    ILogger<PubFileReloadService> logger) : IPubFileReloadService
{
    public async Task<bool> ReloadAsync()
    {
        try
        {
            dataFiles.Reload();
            await RefreshCacheAsync();
            logger.LogInformation("Reloaded pub files");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reload pub files");
            return false;
        }
    }

    public async Task RefreshCacheAsync()
    {
        // Cache Items
        var items = dataFiles.Eif.Items
            .Select((item, index) => new ItemRecord
            {
                Id = index + 1,
                Name = item.Name ?? string.Empty,
                GraphicId = item.GraphicId,
                Type = (int)item.Type,
                SubType = (int)item.Special, // Special is used as subtype
                Special = item.Spec1,
                Hp = item.Hp,
                Tp = item.Tp,
                MinDamage = item.MinDamage,
                MaxDamage = item.MaxDamage,
                Accuracy = item.Accuracy,
                Evade = item.Evade,
                Armor = item.Armor,
                Strength = item.Str,
                Intelligence = item.Intl,
                Wisdom = item.Wis,
                Agility = item.Agi,
                Constitution = item.Con,
                Charisma = item.Cha,
                LevelRequirement = item.LevelRequirement,
                ClassRequirement = item.ClassRequirement,
                Weight = item.Weight
            })
            .Where(i => !string.IsNullOrEmpty(i.Name))
            .ToList();

        await pubCache.CacheItemsAsync(items);
        logger.LogInformation("Cached {Count} items", items.Count);

        // Cache NPCs
        var npcs = dataFiles.Enf.Npcs
            .Select((npc, index) => new NpcRecord
            {
                Id = index + 1,
                Name = npc.Name ?? string.Empty,
                GraphicId = npc.GraphicId,
                Type = (int)npc.Type,
                Hp = npc.Hp,
                Tp = npc.Tp,
                MinDamage = npc.MinDamage,
                MaxDamage = npc.MaxDamage,
                Accuracy = npc.Accuracy,
                Evade = npc.Evade,
                Armor = npc.Armor,
                Experience = npc.Experience
            })
            .Where(n => !string.IsNullOrEmpty(n.Name))
            .ToList();

        await pubCache.CacheNpcsAsync(npcs);
        logger.LogInformation("Cached {Count} NPCs", npcs.Count);

        // Cache Spells
        var spells = dataFiles.Esf.Skills
            .Select((spell, index) => new SpellRecord
            {
                Id = index + 1,
                Name = spell.Name ?? string.Empty,
                Shout = spell.Chant ?? string.Empty,
                IconId = spell.IconId,
                GraphicId = spell.GraphicId,
                TpCost = spell.TpCost,
                SpCost = spell.SpCost,
                CastTime = spell.CastTime,
                Type = (int)spell.Type,
                TargetRestrict = (int)spell.TargetRestrict,
                Target = (int)spell.TargetType,
                MinDamage = spell.MinDamage,
                MaxDamage = spell.MaxDamage,
                Accuracy = spell.Accuracy,
                Hp = spell.HpHeal
            })
            .Where(s => !string.IsNullOrEmpty(s.Name))
            .ToList();

        await pubCache.CacheSpellsAsync(spells);
        logger.LogInformation("Cached {Count} spells", spells.Count);

        // Cache Classes
        var classes = dataFiles.Ecf.Classes
            .Select((cls, index) => new ClassRecord
            {
                Id = index + 1,
                Name = cls.Name ?? string.Empty,
                ParentType = cls.ParentType,
                StatGroup = cls.StatGroup,
                Strength = cls.Str,
                Intelligence = cls.Intl,
                Wisdom = cls.Wis,
                Agility = cls.Agi,
                Constitution = cls.Con,
                Charisma = cls.Cha
            })
            .Where(c => !string.IsNullOrEmpty(c.Name))
            .ToList();

        await pubCache.CacheClassesAsync(classes);
        logger.LogInformation("Cached {Count} classes", classes.Count);

        logger.LogInformation("Pub file caching complete");
    }
}
