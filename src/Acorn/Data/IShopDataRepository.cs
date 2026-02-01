namespace Acorn.Data;

/// <summary>
/// Repository for shop data (trades and crafts)
/// </summary>
public interface IShopDataRepository
{
    /// <summary>
    /// Get shop data by NPC behavior ID
    /// </summary>
    ShopData? GetShopByBehaviorId(int behaviorId);
    
    /// <summary>
    /// Get all shops
    /// </summary>
    IEnumerable<ShopData> GetAllShops();
}
