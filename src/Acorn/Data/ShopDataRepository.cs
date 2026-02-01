using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Acorn.Data;

/// <summary>
/// Loads shop data from JSON files in Data/Shops/ directory
/// </summary>
public class ShopDataRepository : IShopDataRepository
{
    private readonly ILogger<ShopDataRepository> _logger;
    private readonly List<ShopData> _shops = [];
    private const string ShopsDirectory = "Data/Shops";

    public ShopDataRepository(ILogger<ShopDataRepository> logger)
    {
        _logger = logger;
        LoadShops();
    }

    private void LoadShops()
    {
        if (!Directory.Exists(ShopsDirectory))
        {
            _logger.LogWarning("Shops directory not found at {Directory}. Creating with sample shop.", ShopsDirectory);
            Directory.CreateDirectory(ShopsDirectory);
            CreateSampleShop();
            return;
        }

        var jsonFiles = Directory.GetFiles(ShopsDirectory, "*.json");
        if (jsonFiles.Length == 0)
        {
            _logger.LogWarning("No shop files found in {Directory}. Creating sample shop.", ShopsDirectory);
            CreateSampleShop();
            return;
        }

        foreach (var file in jsonFiles)
        {
            try
            {
                var json = File.ReadAllText(file);
                var shopJson = JsonSerializer.Deserialize<ShopJsonModel>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (shopJson == null)
                {
                    _logger.LogWarning("Failed to parse shop file: {File}", file);
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

                _shops.Add(shop);
                _logger.LogInformation("Loaded shop: {Name} (BehaviorId: {BehaviorId}, {TradeCount} trades, {CraftCount} crafts)",
                    shop.Name, shop.BehaviorId, shop.Trades.Count, shop.Crafts.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading shop file: {File}", file);
            }
        }

        _logger.LogInformation("Loaded {Count} shops", _shops.Count);
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
                new { item_id = 1, buy_price = 0, sell_price = 1, max_amount = 99 }
            },
            crafts = Array.Empty<object>()
        };

        var json = JsonSerializer.Serialize(sampleShop, new JsonSerializerOptions { WriteIndented = true });
        var samplePath = Path.Combine(ShopsDirectory, "sample_shop.json");
        File.WriteAllText(samplePath, json);
        _logger.LogInformation("Created sample shop file at {Path}", samplePath);
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
