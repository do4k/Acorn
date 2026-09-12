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
