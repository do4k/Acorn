using System.IO;
using Acorn.Database.Repository;
using Acorn.Game.Models;
using Acorn.Game.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acorn.Tests.Database;

/// <summary>
///     Drop configuration chances are percentages (0-100) and may be fractional, exactly
///     like eoserv's drop values. They must be stored as written: a previous conversion
///     stored <c>chance / 100 * 1000</c> in a field that was then treated as a percentage,
///     so every chance of 10% or more became a guaranteed drop.
/// </summary>
public class DropFileLoaderTests
{
    private static (DropFileLoader Loader, LootService LootService) CreateLoader()
    {
        return (new DropFileLoader(NullLogger<DropFileLoader>.Instance), new LootService());
    }

    private static string WriteTempDropFile(string contents)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, contents);
        return path;
    }

    [Test]
    public void LoadDrops_ShouldKeepChanceAsPercentage()
    {
        var (loader, lootService) = CreateLoader();
        var path = WriteTempDropFile("""
            {
              "npc_drops": [
                { "npc_id": 1, "drops": [ { "item_id": 100, "min_amount": 1, "max_amount": 2, "rate_percent": 10 } ] }
              ]
            }
            """);

        try
        {
            loader.LoadDrops(lootService, path);

            var drop = lootService.GetNpcLootTable(1)!.Drops.Should().ContainSingle().Subject;
            drop.RatePercent.Should().Be(10, "rate_percent 10 means 10 percent, not a guaranteed drop");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void LoadDrops_ShouldParseFractionalChances()
    {
        var (loader, lootService) = CreateLoader();
        var path = WriteTempDropFile("""
            {
              "npc_drops": [
                { "npc_id": 1, "drops": [ { "item_id": 100, "min_amount": 1, "max_amount": 2, "rate_percent": 0.5 } ] }
              ]
            }
            """);

        try
        {
            loader.LoadDrops(lootService, path);

            lootService.GetNpcLootTable(1)!.Drops.Should().ContainSingle()
                .Which.RatePercent.Should().Be(0.5);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void LoadDrops_ShouldSkipInvalidEntriesButKeepTheRestOfTheTable()
    {
        var (loader, lootService) = CreateLoader();
        var path = WriteTempDropFile("""
            {
              "npc_drops": [
                {
                  "npc_id": 1,
                  "drops": [
                    { "item_id": 0, "min_amount": 1, "max_amount": 1, "rate_percent": 5 },
                    { "item_id": 5, "min_amount": 9, "max_amount": 2, "rate_percent": 5 },
                    { "item_id": 6, "min_amount": 1, "max_amount": 1, "rate_percent": 250 },
                    { "item_id": 7, "min_amount": 1, "max_amount": 3, "rate_percent": 25 }
                  ]
                }
              ]
            }
            """);

        try
        {
            loader.LoadDrops(lootService, path);

            // item 0, the inverted range and the 250% rate are each rejected; the valid
            // entry in the same table still loads. The text loader would have silently
            // dropped the whole line on a bad token.
            lootService.GetNpcLootTable(1)!.Drops.Should().ContainSingle()
                .Which.Should().Match<LootDrop>(d => d.ItemId == 7 && d.MinAmount == 1 && d.MaxAmount == 3);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void LoadDrops_WhenNpcTableIsRepeated_ShouldKeepTheFirstTableAndWarn()
    {
        var (loader, lootService) = CreateLoader();
        var path = WriteTempDropFile("""
            {
              "npc_drops": [
                { "npc_id": 9, "drops": [ { "item_id": 11, "min_amount": 1, "max_amount": 1, "rate_percent": 5 } ] },
                { "npc_id": 9, "drops": [ { "item_id": 22, "min_amount": 1, "max_amount": 1, "rate_percent": 5 } ] }
              ]
            }
            """);

        try
        {
            loader.LoadDrops(lootService, path);

            lootService.GetNpcLootTable(9)!.Drops.Should().ContainSingle()
                .Which.ItemId.Should().Be(11);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void LoadDrops_WhenJsonIsMalformed_ShouldLoadNothingAndNotThrow()
    {
        var (loader, lootService) = CreateLoader();
        var path = WriteTempDropFile("{ this is not json ");

        try
        {
            var act = () => loader.LoadDrops(lootService, path);

            act.Should().NotThrow();
            lootService.GetNpcLootTable(1).Should().BeNull();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void LoadDrops_WhenFileIsMissing_ShouldLoadNothingAndNotThrow()
    {
        var (loader, lootService) = CreateLoader();
        var missing = Path.Combine(Path.GetTempPath(), "no-such-drops-" + Guid.NewGuid().ToString("N") + ".json");

        var act = () => loader.LoadDrops(lootService, missing);

        act.Should().NotThrow();
        lootService.GetNpcLootTable(1).Should().BeNull();
    }

    [Test]
    public void LoadGlobalDrops_ShouldParseChances()
    {
        var (loader, lootService) = CreateLoader();
        var path = WriteTempDropFile("""
            {
              "global_drops": [ { "item_id": 1, "min_amount": 1, "max_amount": 5, "rate_percent": 15 } ]
            }
            """);

        try
        {
            loader.LoadGlobalDrops(lootService, path);

            // Global drops join the NPC roll; roll until the 15% chance hits so the
            // parsed entry can be inspected.
            LootDrop? result = null;
            for (var i = 0; i < 200 && result is null; i++)
            {
                result = lootService.RollDrop(npcId: 1);
            }

            result.Should().NotBeNull();
            result!.ItemId.Should().Be(1);
            result.RatePercent.Should().Be(15);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
