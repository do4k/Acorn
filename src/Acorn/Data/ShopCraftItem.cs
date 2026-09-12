namespace Acorn.Data;

/// <summary>
/// Represents a craftable item
/// </summary>
public record ShopCraftItem(
    int ItemId,
    List<ShopCraftIngredient> Ingredients
);
