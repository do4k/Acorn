using Acorn.Domain.Models;
using Acorn.Game.Services;
using Microsoft.Extensions.Logging;

namespace Acorn.Database.Repository;

/// <summary>
///     Loads NPC drop data from a text configuration file.
///     Format: npc_id = item_id,min,max,chance, item_id,min,max,chance, ...
///     Example: 1 = 248,1,2,10, 249,1,1,5
///     Lines starting with # are comments.
/// </summary>
public class DropFileTextLoader
{
    private readonly ILogger<DropFileTextLoader> _logger;

    public DropFileTextLoader(ILogger<DropFileTextLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Load drops from text config file and register them with the loot service
    ///     Format: npc_id = item_id,min,max,chance, item_id,min,max,chance, ...
    ///     Example: 1 = 248,1,2,10
    ///     Example: 2 = 1,1,10,10, 249,1,1,10
    /// </summary>
    public void LoadDrops(ILootService lootService, string configFilePath)
    {
        if (!File.Exists(configFilePath))
        {
            _logger.LogWarning("Drop config file not found: {FilePath}", configFilePath);
            return;
        }

        try
        {
            var lines = File.ReadAllLines(configFilePath);
            var npcDrops = new Dictionary<int, List<LootDrop>>();
            var lineNum = 0;
            var totalDrops = 0;

            foreach (var line in lines)
            {
                lineNum++;
                var trimmed = line.Trim();

                // Skip empty lines and comments
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                {
                    continue;
                }

                // Parse format: npc_id = item,min,max,chance, item,min,max,chance, ...
                var parts = trimmed.Split('=', 2);
                if (parts.Length != 2)
                {
                    _logger.LogWarning("Invalid drop line {LineNum}: Expected 'npc_id = drops' format", lineNum);
                    continue;
                }

                if (!int.TryParse(parts[0].Trim(), out var npcId))
                {
                    _logger.LogWarning("Invalid drop line {LineNum}: Could not parse NPC ID", lineNum);
                    continue;
                }

                var dropsStr = parts[1].Trim();
                if (string.IsNullOrWhiteSpace(dropsStr))
                {
                    // Empty drop list for this NPC
                    continue;
                }

                // Split drops by comma (each drop is 4 values: item,min,max,chance)
                var values = dropsStr.Split(',').Select(v => v.Trim()).ToArray();
                if (values.Length % 4 != 0)
                {
                    _logger.LogWarning(
                        "Invalid drop line {LineNum}: Drop values must be in groups of 4 (item,min,max,chance), got {Count}",
                        lineNum, values.Length);
                    continue;
                }

                if (!npcDrops.ContainsKey(npcId))
                {
                    npcDrops[npcId] = new List<LootDrop>();
                }

                // Parse each drop (4 values per drop)
                for (var i = 0; i < values.Length; i += 4)
                {
                    if (!int.TryParse(values[i], out var itemId) ||
                        !int.TryParse(values[i + 1], out var minAmount) ||
                        !int.TryParse(values[i + 2], out var maxAmount) ||
                        !float.TryParse(values[i + 3], out var chance))
                    {
                        _logger.LogWarning(
                            "Invalid drop on line {LineNum}: Could not parse drop values at position {Pos}",
                            lineNum, i);
                        continue;
                    }

                    // Convert chance from 0-100 percentage to internal 0-64000 range
                    var rate = (int)Math.Floor(chance / 100f * 64000f);

                    npcDrops[npcId].Add(new LootDrop
                    {
                        ItemId = itemId,
                        MinAmount = minAmount,
                        MaxAmount = maxAmount,
                        RatePercent = Math.Min(100, rate / 640)
                    });

                    totalDrops++;
                }
            }

            // Register all NPC loot tables
            foreach (var kvp in npcDrops)
            {
                var lootTable = new NpcLootTable
                {
                    NpcId = kvp.Key,
                    Drops = kvp.Value
                };
                lootService.RegisterNpcLootTable(lootTable);
            }

            _logger.LogInformation("Loaded {NpcCount} NPC loot tables with {DropCount} total drops from {FilePath}",
                npcDrops.Count, totalDrops, configFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading drop config file from {FilePath}", configFilePath);
        }
    }
}