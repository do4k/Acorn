namespace Acorn.Data;

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
