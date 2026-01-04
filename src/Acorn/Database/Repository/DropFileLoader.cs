using Acorn.Game.Models;
using Acorn.Game.Services;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Data;

namespace Acorn.Database.Repository;

/// <summary>
/// Loads NPC drop data from the EDF (drop table) binary file.
/// Format: short npc_id → short num_drops → per drop: short item_id, 3-byte min/max amounts, 2-byte rate
/// </summary>
public class DropFileLoader
{
    private readonly ILogger _logger;

    public DropFileLoader(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Load drops from EDF file and register them with the loot service
    /// </summary>
    public void LoadDrops(ILootService lootService, string edfFilePath)
    {
        if (!File.Exists(edfFilePath))
        {
            _logger.LogWarning("Drop file not found: {FilePath}", edfFilePath);
            return;
        }

        try
        {
            var fileData = File.ReadAllBytes(edfFilePath);
            var reader = new EoReader(fileData);

            int npcCount = 0;
            int dropCount = 0;

            // Read until we can't read a complete NPC entry
            while (reader.Position + 4 <= fileData.Length)
            {
                // Read NPC ID
                var npcId = reader.GetShort();
                if (npcId == 0)
                    break;

                // Read number of drops for this NPC
                var numDrops = reader.GetShort();
                var drops = new List<LootDrop>();

                for (int i = 0; i < numDrops; i++)
                {
                    // Check if we can read the drop data (2 + 3 + 3 + 2 = 10 bytes)
                    if (reader.Position + 10 > fileData.Length)
                        break;

                    // Read item ID
                    var itemId = reader.GetShort();

                    // Read min amount (3 bytes)
                    var minBytes = reader.GetBytes(3);
                    int minAmount = minBytes[0] | (minBytes[1] << 8) | (minBytes[2] << 16);

                    // Read max amount (3 bytes)
                    var maxBytes = reader.GetBytes(3);
                    int maxAmount = maxBytes[0] | (maxBytes[1] << 8) | (maxBytes[2] << 16);

                    // Read rate (2 bytes, 0-64000 range, convert to 0-100 percent)
                    var rateValue = reader.GetShort();
                    // Convert from 0-64000 range to 0-100 percent (rate / 640 = percent)
                    int ratePercent = Math.Min(100, rateValue / 640);

                    drops.Add(new LootDrop
                    {
                        ItemId = itemId,
                        MinAmount = minAmount,
                        MaxAmount = maxAmount,
                        RatePercent = ratePercent
                    });

                    dropCount++;
                }

                // Register this NPC's loot table
                if (drops.Count > 0)
                {
                    var lootTable = new NpcLootTable
                    {
                        NpcId = npcId,
                        Drops = drops
                    };
                    lootService.RegisterNpcLootTable(lootTable);
                    npcCount++;
                }
            }

            _logger.LogInformation("Loaded {NpcCount} NPC loot tables with {DropCount} total drops from {FilePath}",
                npcCount, dropCount, edfFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading drop file from {FilePath}", edfFilePath);
        }
    }
}
