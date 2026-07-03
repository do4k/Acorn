using Acorn.Game.Models;
using Acorn.Game.Services;
using Microsoft.Extensions.Logging;

namespace Acorn.Database.Repository;

/// <summary>
///     Loads NPC loot tables from text configuration files.
///     Format: npc_id = item_id,min,max,chance, item_id,min,max,chance, ...
/// </summary>
public class DropFileTextLoader
{
    private readonly ILogger<DropFileTextLoader> _logger;

    public DropFileTextLoader(ILogger<DropFileTextLoader> logger)
    {
        _logger = logger;
    }

    public void LoadDrops(ILootService lootService, string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Drop file not found: {FilePath}", filePath);
                return;
            }

            var lines = File.ReadAllLines(filePath);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                    continue;

                var parts = line.Split('=');
                if (parts.Length != 2)
                    continue;

                if (!int.TryParse(parts[0].Trim(), out var npcId))
                    continue;

                var dropParts = parts[1].Split(',');
                var lootDrops = new List<LootDrop>();

                for (var i = 0; i < dropParts.Length; i += 4)
                {
                    if (i + 3 >= dropParts.Length)
                        break;

                    if (!int.TryParse(dropParts[i].Trim(), out var itemId) ||
                        !int.TryParse(dropParts[i + 1].Trim(), out var min) ||
                        !int.TryParse(dropParts[i + 2].Trim(), out var max) ||
                        !int.TryParse(dropParts[i + 3].Trim(), out var chance))
                        continue;

                    // Convert chance from 0-100 to internal rate system (1-1000)
                    var rate = Math.Max(1, (int)(chance / 100.0 * 1000));

                    lootDrops.Add(new LootDrop
                    {
                        ItemId = itemId,
                        MinAmount = min,
                        MaxAmount = max,
                        RatePercent = rate
                    });
                }

                if (lootDrops.Count > 0)
                {
                    var npcLootTable = new NpcLootTable
                    {
                        NpcId = npcId,
                        Drops = lootDrops
                    };
                    lootService.RegisterNpcLootTable(npcLootTable);
                }
            }

            _logger.LogInformation("Loaded drops from {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading drops from {FilePath}", filePath);
        }
    }

    /// <summary>
    ///     Loads drops that apply to every NPC (e.g. a universal gold chance) from a text
    ///     configuration file.
    ///     Format: item_id,min,max,chance
    /// </summary>
    public void LoadGlobalDrops(ILootService lootService, string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Global drop file not found: {FilePath}", filePath);
                return;
            }

            var lines = File.ReadAllLines(filePath);
            var globalDrops = new List<LootDrop>();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("//"))
                    continue;

                var parts = line.Split(',');
                if (parts.Length != 4)
                    continue;

                if (!int.TryParse(parts[0].Trim(), out var itemId) ||
                    !int.TryParse(parts[1].Trim(), out var min) ||
                    !int.TryParse(parts[2].Trim(), out var max) ||
                    !int.TryParse(parts[3].Trim(), out var chance))
                    continue;

                globalDrops.Add(new LootDrop
                {
                    ItemId = itemId,
                    MinAmount = min,
                    MaxAmount = max,
                    RatePercent = chance
                });
            }

            if (globalDrops.Count > 0)
            {
                lootService.RegisterGlobalDrops(globalDrops);
            }

            _logger.LogInformation("Loaded {Count} global drops from {FilePath}", globalDrops.Count, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading global drops from {FilePath}", filePath);
        }
    }
}

