using Acorn.Database.Repository;
using Acorn.Shared.Caching;
using Acorn.Shared.Models.Pub;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Acorn.Infrastructure;

/// <summary>
/// Hosted service that caches pub file data to Redis on startup.
/// </summary>
public class PubFileCacheHostedService : IHostedService
{
    private readonly IDataFileRepository _dataFiles;
    private readonly IPubCacheService _pubCache;
    private readonly ILogger<PubFileCacheHostedService> _logger;

    public PubFileCacheHostedService(
        IDataFileRepository dataFiles,
        IPubCacheService pubCache,
        ILogger<PubFileCacheHostedService> logger)
    {
        _dataFiles = dataFiles;
        _pubCache = pubCache;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Caching pub files to Redis...");

        try
        {
            // Cache Items
            var items = _dataFiles.Eif.Items
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

            await _pubCache.CacheItemsAsync(items);
            _logger.LogInformation("Cached {Count} items", items.Count);

            // Cache NPCs
            var npcs = _dataFiles.Enf.Npcs
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

            await _pubCache.CacheNpcsAsync(npcs);
            _logger.LogInformation("Cached {Count} NPCs", npcs.Count);

            // Cache Spells
            var spells = _dataFiles.Esf.Skills
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

            await _pubCache.CacheSpellsAsync(spells);
            _logger.LogInformation("Cached {Count} spells", spells.Count);

            // Cache Classes
            var classes = _dataFiles.Ecf.Classes
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

            await _pubCache.CacheClassesAsync(classes);
            _logger.LogInformation("Cached {Count} classes", classes.Count);

            _logger.LogInformation("Pub file caching complete");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache pub files");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

