using Acorn.Data;
using Acorn.Database.Repository;
using Acorn.Options;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Tests.Data;

/// <summary>
///     Validates the real shop data in Data/Shops against the real pub files.
///     Game data is per-deployment content (git-ignored, bind-mounted into the
///     containers), so the tests skip when a checkout has no data.
///     <see cref="ShopDataRepository" /> resolves "Data/Shops" relative to the process
///     working directory, so the tests temporarily switch to the server project directory.
/// </summary>
public class ShopDataRepositoryTests
{
    /// <summary>
    ///     Shared across the tests in this class: parsing the pub files is a few
    ///     hundred ms, and doing it per test adds needless load to the suite.
    /// </summary>
    private static readonly Lazy<(ShopDataRepository Repository, DataFileRepository DataFiles)> RealData =
        new(LoadRealData);

    private static bool HasDeploymentData =>
        TryFindServerProjectDirectory(out var serverDirectory) &&
        File.Exists(Path.Combine(serverDirectory, "Data", "pub", "dat001.eif")) &&
        File.Exists(Path.Combine(serverDirectory, "Data", "pub", "dtn001.enf")) &&
        Directory.EnumerateFiles(Path.Combine(serverDirectory, "Data", "Shops"), "*.json").Any();

    private static (ShopDataRepository Repository, DataFileRepository DataFiles) LoadRealData()
    {
        if (!TryFindServerProjectDirectory(out var serverDirectory))
        {
            throw new DirectoryNotFoundException("Could not locate src/Acorn/Data/Shops from the test assembly directory.");
        }

        var originalDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(serverDirectory);

            // MapsPath points nowhere on purpose: this class only needs the pub
            // files, and skipping ~150 .emf parses keeps the suite quick.
            var dataFiles = new DataFileRepository(
                Microsoft.Extensions.Options.Options.Create(new DataOptions { MapsPath = "Data/__no_maps__" }),
                NullLogger<DataFileRepository>.Instance);

            var repository = new ShopDataRepository(
                NullLogger<ShopDataRepository>.Instance,
                dataFiles);

            return (repository, dataFiles);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }

    private static bool TryFindServerProjectDirectory(out string serverDirectory)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Acorn");
            if (Directory.Exists(Path.Combine(candidate, "Data", "Shops")))
            {
                serverDirectory = candidate;
                return true;
            }

            directory = directory.Parent;
        }

        serverDirectory = string.Empty;
        return false;
    }

    [Test]
    public void Load_WhenRealShipsPresent_ShouldDefineAShopForEveryShopNpc()
    {
        Skip.Unless(HasDeploymentData, "per-deployment game data (pub files, Data/Shops) is not in this checkout");

        // Arrange & Act
        var (repository, dataFiles) = RealData.Value;

        // Assert - every Shop-type NPC in the ENF resolves to a configured shop
        var vendorIds = dataFiles.Enf.Npcs
            .Where(npc => npc.Type == NpcType.Shop)
            .Select(npc => npc.BehaviorId)
            .Distinct()
            .ToList();

        vendorIds.Should().NotBeEmpty();

        foreach (var vendorId in vendorIds)
        {
            repository.GetShopByBehaviorId(vendorId)
                .Should().NotBeNull($"shop NPC vendor id {vendorId} needs a Data/Shops entry");
        }
    }

    [Test]
    public void Trades_WhenRealShipsLoaded_ShouldHaveNoInfiniteMoneyOrUnknownItems()
    {
        Skip.Unless(HasDeploymentData, "per-deployment game data (pub files, Data/Shops) is not in this checkout");

        // Arrange & Act
        var (repository, dataFiles) = RealData.Value;

        // Assert
        foreach (var shop in repository.GetAllShops())
        {
            foreach (var trade in shop.Trades)
            {
                dataFiles.Eif.GetItem(trade.ItemId)
                    .Should().NotBeNull($"{shop.Name}: trade item {trade.ItemId} missing from EIF");
                trade.BuyPrice.Should().BeGreaterThanOrEqualTo(0);
                trade.SellPrice.Should().BeGreaterThanOrEqualTo(0);
                if (trade.BuyPrice > 0)
                {
                    trade.SellPrice.Should().BeLessThan(trade.BuyPrice,
                        $"{shop.Name}: sell >= buy for item {trade.ItemId} is an infinite money exploit");
                }
            }

            foreach (var craft in shop.Crafts)
            {
                dataFiles.Eif.GetItem(craft.ItemId)
                    .Should().NotBeNull($"{shop.Name}: craft item {craft.ItemId} missing from EIF");
                craft.Ingredients.Should().HaveCountLessThanOrEqualTo(4,
                    $"{shop.Name}: the protocol only carries 4 ingredient slots");

                foreach (var ingredient in craft.Ingredients)
                {
                    ingredient.ItemId.Should().BeGreaterThan(0);
                    ingredient.Amount.Should().BeGreaterThan(0);
                    dataFiles.Eif.GetItem(ingredient.ItemId)
                        .Should().NotBeNull($"{shop.Name}: ingredient {ingredient.ItemId} missing from EIF");
                }
            }
        }
    }

    [Test]
    public void BehaviorIds_WhenRealShipsLoaded_ShouldBeUnique()
    {
        Skip.Unless(HasDeploymentData, "per-deployment game data (pub files, Data/Shops) is not in this checkout");

        // Arrange & Act
        var (repository, _) = RealData.Value;

        // Assert
        var ids = repository.GetAllShops().Select(s => s.BehaviorId).ToList();
        ids.Should().OnlyHaveUniqueItems();
    }
}
