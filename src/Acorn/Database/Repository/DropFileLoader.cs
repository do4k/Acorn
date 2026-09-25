using System.Text.Json;
using Acorn.Game.Models;
using Acorn.Game.Services;
using Microsoft.Extensions.Logging;

namespace Acorn.Database.Repository;

/// <summary>
///     Loads NPC loot tables from the JSON configuration files
///     <c>Data/drops.json</c> and <c>Data/global_drops.json</c>.
/// </summary>
public class DropFileLoader
{
    private readonly ILogger<DropFileLoader> _logger;

    public DropFileLoader(ILogger<DropFileLoader> logger)
    {
        _logger = logger;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        // Data files use snake_case keys (npc_id, item_id, rate_percent, ...)
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <summary>
    ///     Loads the per-NPC drop tables. <c>drops.json</c> has the shape
    ///     <c>{ "npc_drops": [ { "npc_id": 1, "drops": [ { "item_id", "min_amount",
    ///     "max_amount", "rate_percent" } ] } ] }</c>.
    /// </summary>
    public void LoadDrops(ILootService lootService, string filePath)
    {
        var tables = ReadNpcDrops(filePath);
        if (tables is null)
        {
            return;
        }

        var registered = 0;
        var skipped = 0;

        foreach (var (npcId, drops) in tables)
        {
            if (drops.Count == 0)
            {
                _logger.LogWarning("Drop table for npc {NpcId} has no drops, skipping", npcId);
                skipped++;
                continue;
            }

            lootService.RegisterNpcLootTable(new NpcLootTable
            {
                NpcId = npcId,
                Drops = drops
            });
            registered++;
        }

        _logger.LogInformation(
            "Loaded {Count} NPC drop tables with {DropCount} entries from {FilePath} ({Skipped} empty)",
            registered, tables.Sum(t => t.Item2.Count), filePath, skipped);
    }

    /// <summary>
    ///     Loads drops that apply to every NPC (e.g. a universal gold chance) from
    ///     <c>Data/global_drops.json</c>, shaped as
    ///     <c>{ "global_drops": [ { "item_id", "min_amount", "max_amount", "rate_percent" } ] }</c>.
    /// </summary>
    public void LoadGlobalDrops(ILootService lootService, string filePath)
    {
        var drops = ReadGlobalDrops(filePath);
        if (drops is null || drops.Count == 0)
        {
            return;
        }

        lootService.RegisterGlobalDrops(drops);
        _logger.LogInformation("Loaded {Count} global drops from {FilePath}", drops.Count, filePath);
    }

    private List<(int NpcId, List<LootDrop> Drops)>? ReadNpcDrops(string filePath)
    {
        var document = Read<NpcDropsDocument>(filePath, "npc_drops");
        if (document?.NpcDrops is null)
        {
            return null;
        }

        var result = new List<(int, List<LootDrop>)>(document.NpcDrops.Count);
        var seen = new HashSet<int>();

        foreach (var table in document.NpcDrops)
        {
            if (table.NpcId <= 0)
            {
                _logger.LogWarning("Drop table with invalid npc id {NpcId}, skipping", table.NpcId);
                continue;
            }

            if (!seen.Add(table.NpcId))
            {
                // The old text loader silently kept the last table for a repeated id.
                _logger.LogWarning("Duplicate drop table for npc {NpcId}, keeping the first one", table.NpcId);
                continue;
            }

            var drops = new List<LootDrop>();
            foreach (var drop in table.Drops ?? [])
            {
                if (!IsUsable(drop, table.NpcId))
                {
                    continue;
                }

                drops.Add(drop.ToLootDrop());
            }

            result.Add((table.NpcId, drops));
        }

        return result;
    }

    private List<LootDrop>? ReadGlobalDrops(string filePath)
    {
        var document = Read<GlobalDropsDocument>(filePath, "global_drops");
        if (document?.GlobalDrops is null)
        {
            return null;
        }

        var drops = new List<LootDrop>();
        foreach (var drop in document.GlobalDrops)
        {
            if (IsUsable(drop, npcId: null))
            {
                drops.Add(drop.ToLootDrop());
            }
        }

        return drops;
    }

    private T? Read<T>(string filePath, string rootProperty) where T : class
    {
        if (!File.Exists(filePath))
        {
            _logger.LogError(
                "Drop file not found: {FilePath}. NPC loot tables will be empty until it is restored",
                filePath);
            return null;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Could not parse {FilePath} ({RootProperty}); no drops were loaded", filePath,
                rootProperty);
            return null;
        }
    }

    private bool IsUsable(DropJsonModel drop, int? npcId)
    {
        var where = npcId is { } id ? $"npc {id}" : "global drops";
        var valid = true;

        if (drop.ItemId <= 0)
        {
            _logger.LogWarning("Drop entry in {Where} has invalid item id {ItemId}, skipping", where, drop.ItemId);
            valid = false;
        }

        if (drop.MinAmount < 0 || drop.MaxAmount < drop.MinAmount)
        {
            _logger.LogWarning("Drop entry for item {ItemId} in {Where} has an impossible range " +
                               "{Min}..{Max}, skipping", drop.ItemId, where, drop.MinAmount, drop.MaxAmount);
            valid = false;
        }

        if (drop.RatePercent is < 0 or > 100)
        {
            _logger.LogWarning("Drop entry for item {ItemId} in {Where} has rate {Rate} outside 0..100, skipping",
                drop.ItemId, where, drop.RatePercent);
            valid = false;
        }

        return valid;
    }

    private class NpcDropsDocument
    {
        public List<NpcDropTableJsonModel>? NpcDrops { get; set; }
    }

    private class NpcDropTableJsonModel
    {
        public int NpcId { get; set; }
        public List<DropJsonModel>? Drops { get; set; }
    }

    private class GlobalDropsDocument
    {
        public List<DropJsonModel>? GlobalDrops { get; set; }
    }

    private class DropJsonModel
    {
        public int ItemId { get; set; }
        public int MinAmount { get; set; }
        public int MaxAmount { get; set; }
        public double RatePercent { get; set; }

        public LootDrop ToLootDrop() => new(ItemId, MinAmount, MaxAmount, RatePercent);
    }
}
