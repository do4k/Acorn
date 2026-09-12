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
public class DropFileTextLoaderTests
{
    private static (DropFileTextLoader Loader, LootService LootService) CreateLoader()
    {
        return (new DropFileTextLoader(NullLogger<DropFileTextLoader>.Instance), new LootService());
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
        var path = WriteTempDropFile("1 = 100,1,2,10");

        try
        {
            loader.LoadDrops(lootService, path);

            var drop = lootService.GetNpcLootTable(1)!.Drops.Should().ContainSingle().Subject;
            drop.RatePercent.Should().Be(10, "10 in drops.txt means 10 percent, not a guaranteed drop");
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
        var path = WriteTempDropFile("1 = 100,1,2,0.5");

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
    public void LoadDrops_ShouldIgnoreMalformedLines()
    {
        var (loader, lootService) = CreateLoader();
        var path = WriteTempDropFile("not a drop line\n// comment\n2 = 1,1,1,25");

        try
        {
            loader.LoadDrops(lootService, path);

            lootService.GetNpcLootTable(2)!.Drops.Should().ContainSingle()
                .Which.RatePercent.Should().Be(25);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void LoadGlobalDrops_ShouldParseChances()
    {
        var (loader, lootService) = CreateLoader();
        var path = WriteTempDropFile("1,1,5,15");

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