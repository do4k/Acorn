namespace Acorn.Data;

/// <summary>
/// Represents a shop trade item (buy/sell)
/// </summary>
public record ShopTradeItem(
    int ItemId,
    int BuyPrice,
    int SellPrice,
    int MaxAmount
);

/// <summary>
/// Represents an ingredient for crafting
/// </summary>
public record ShopCraftIngredient(
    int ItemId,
    int Amount
);

/// <summary>
/// Represents a craftable item
/// </summary>
public record ShopCraftItem(
    int ItemId,
    List<ShopCraftIngredient> Ingredients
);

/// <summary>
/// Represents a shop configuration
/// </summary>
public record ShopData(
    int BehaviorId,
    string Name,
    int MinLevel,
    int MaxLevel,
    int ClassRequirement,
    List<ShopTradeItem> Trades,
    List<ShopCraftItem> Crafts
);
