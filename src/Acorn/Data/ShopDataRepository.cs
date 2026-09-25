using System.Text.Json;
using Acorn.Database.Repository;
using Acorn.Infrastructure.Telemetry;
using Microsoft.Extensions.Logging;

namespace Acorn.Data;

/// <summary>
/// Loads shop data from JSON files in Data/Shops/ directory
/// </summary>
public class ShopDataRepository : IShopDataRepository
{
    private readonly ILogger<ShopDataRepository> _logger;
    private readonly IDataFileRepository _dataFileRepository;
    private readonly List<ShopData> _shops = [];
    private const string ShopsDirectory = "Data/Shops";
    private const int MaxCraftIngredients = 4;

    public ShopDataRepository(ILogger<ShopDataRepository> logger, IDataFileRepository dataFileRepository)
    {
        _logger = logger;
        _dataFileRepository = dataFileRepository;
        LoadShops();
    }

    private void LoadShops()
    {
        if (!Directory.Exists(ShopsDirectory))
        {
            try
            {
                _logger.DataDirectoryNotFound(ShopsDirectory);
                Directory.CreateDirectory(ShopsDirectory);
                CreateSampleShop();
            }
            catch (IOException ex)
            {
                _logger.DataDirectoryCreateFailed(ex, ShopsDirectory);
                return;
            }
        }

        var jsonFiles = Directory.GetFiles(ShopsDirectory, "*.json");
        if (jsonFiles.Length == 0)
        {
            _logger.DataDirectoryEmpty(ShopsDirectory);
            try
            {
                CreateSampleShop();
            }
            catch (IOException ex)
            {
                _logger.DataDirectoryCreateFailed(ex, ShopsDirectory);
                return;
            }

            jsonFiles = Directory.GetFiles(ShopsDirectory, "*.json");
        }

        foreach (var file in jsonFiles)
        {
            try
            {
                var json = File.ReadAllText(file);
                var shopJson = JsonSerializer.Deserialize<ShopJsonModel>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    // Data files use snake_case keys (behavior_id, item_id, ...)
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                });

                if (shopJson == null)
                {
                    _logger.DataFileParseFailed(file);
                    continue;
                }

                var shop = new ShopData(
                    shopJson.BehaviorId,
                    shopJson.Name ?? "Unknown Shop",
                    shopJson.MinLevel,
                    shopJson.MaxLevel,
                    shopJson.ClassRequirement,
                    shopJson.Trades?.Select(t => new ShopTradeItem(
                        t.ItemId,
                        t.BuyPrice,
                        t.SellPrice,
                        t.MaxAmount > 0 ? t.MaxAmount : 99
                    )).ToList() ?? [],
                    shopJson.Crafts?.Select(c => new ShopCraftItem(
                        c.ItemId,
                        c.Ingredients?.Select(i => new ShopCraftIngredient(i.ItemId, i.Amount)).ToList() ?? []
                    )).ToList() ?? []
                );

                shop = ValidateShop(shop);

                if (_shops.Any(s => s.BehaviorId == shop.BehaviorId))
                {
                    _logger.ShopDataValidationFailed(shop.Name, shop.BehaviorId,
                        "duplicate behavior ID, keeping the first shop loaded");
                    continue;
                }

                _shops.Add(shop);
                _logger.ShopLoaded(shop.Name, shop.BehaviorId, shop.Trades.Count, shop.Crafts.Count);
            }
            catch (Exception ex)
            {
                _logger.DataFileLoadError(ex, file);
            }
        }

        _logger.ShopsLoaded(_shops.Count);
    }

    /// <summary>
    /// Validates a shop against the item database and common misconfigurations,
    /// logging warnings. Returns a corrected copy where necessary (e.g. truncated
    /// craft ingredient lists, which the protocol limits to 4).
    /// </summary>
    private ShopData ValidateShop(ShopData shop)
    {
        foreach (var trade in shop.Trades)
        {
            if (_dataFileRepository.Eif.GetItem(trade.ItemId) == null)
            {
                _logger.ShopDataValidationFailed(shop.Name, shop.BehaviorId,
                    $"trade item {trade.ItemId} does not exist in the EIF");
            }

            if (trade.BuyPrice < 0 || trade.SellPrice < 0)
            {
                _logger.ShopDataValidationFailed(shop.Name, shop.BehaviorId,
                    $"trade item {trade.ItemId} has a negative price");
            }

            if (trade.BuyPrice > 0 && trade.SellPrice > trade.BuyPrice)
            {
                _logger.ShopDataValidationFailed(shop.Name, shop.BehaviorId,
                    $"trade item {trade.ItemId} sells for more than it costs (infinite money exploit)");
            }
        }

        var crafts = shop.Crafts;
        foreach (var craft in crafts)
        {
            if (_dataFileRepository.Eif.GetItem(craft.ItemId) == null)
            {
                _logger.ShopDataValidationFailed(shop.Name, shop.BehaviorId,
                    $"craft item {craft.ItemId} does not exist in the EIF");
            }

            foreach (var ingredient in craft.Ingredients.Where(i => i.ItemId > 0))
            {
                if (ingredient.Amount <= 0)
                {
                    _logger.ShopDataValidationFailed(shop.Name, shop.BehaviorId,
                        $"craft item {craft.ItemId} has ingredient {ingredient.ItemId} with invalid amount {ingredient.Amount}");
                }

                if (_dataFileRepository.Eif.GetItem(ingredient.ItemId) == null)
                {
                    _logger.ShopDataValidationFailed(shop.Name, shop.BehaviorId,
                        $"craft item {craft.ItemId} has ingredient {ingredient.ItemId} which does not exist in the EIF");
                }
            }
        }

        if (crafts.Any(c => c.Ingredients.Count > MaxCraftIngredients))
        {
            _logger.ShopDataValidationFailed(shop.Name, shop.BehaviorId,
                $"one or more crafts have more than {MaxCraftIngredients} ingredients, truncating");

            crafts = crafts
                .Select(c => c.Ingredients.Count > MaxCraftIngredients
                    ? c with { Ingredients = c.Ingredients.Take(MaxCraftIngredients).ToList() }
                    : c)
                .ToList();
        }

        return crafts == shop.Crafts ? shop : shop with { Crafts = crafts };
    }

    private void CreateSampleShop()
    {
        var sampleShop = new
        {
            behavior_id = 1,
            name = "Sample Shop",
            min_level = 0,
            max_level = 0,
            class_requirement = 0,
            trades = new[]
            {
                // 3 = Health Potion. buy_price is what the player pays, sell_price
                // is what the shop pays back; 0 in either field means "not sold"
                // / "not bought" respectively.
                new { item_id = 3, buy_price = 25, sell_price = 10, max_amount = 99 }
            },
            crafts = Array.Empty<object>()
        };

        var json = JsonSerializer.Serialize(sampleShop, new JsonSerializerOptions { WriteIndented = true });
        var samplePath = Path.Combine(ShopsDirectory, "sample_shop.json");
        File.WriteAllText(samplePath, json);
        _logger.SampleDataFileCreated(samplePath);
    }

    public ShopData? GetShopByBehaviorId(int behaviorId)
    {
        return _shops.FirstOrDefault(s => s.BehaviorId == behaviorId);
    }

    public IEnumerable<ShopData> GetAllShops()
    {
        return _shops;
    }

    // JSON model classes for deserialization
    private class ShopJsonModel
    {
        public int BehaviorId { get; set; }
        public string? Name { get; set; }
        public int MinLevel { get; set; }
        public int MaxLevel { get; set; }
        public int ClassRequirement { get; set; }
        public List<TradeJsonModel>? Trades { get; set; }
        public List<CraftJsonModel>? Crafts { get; set; }
    }

    private class TradeJsonModel
    {
        public int ItemId { get; set; }
        public int BuyPrice { get; set; }
        public int SellPrice { get; set; }
        public int MaxAmount { get; set; }
    }

    private class CraftJsonModel
    {
        public int ItemId { get; set; }
        public List<IngredientJsonModel>? Ingredients { get; set; }
    }

    private class IngredientJsonModel
    {
        public int ItemId { get; set; }
        public int Amount { get; set; }
    }
}
