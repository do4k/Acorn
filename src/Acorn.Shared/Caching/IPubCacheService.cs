using Acorn.Shared.Models.Pub;

namespace Acorn.Shared.Caching;

/// <summary>
/// Service for caching and retrieving pub file data from Redis.
/// </summary>
public interface IPubCacheService
{
    // Items
    Task CacheItemsAsync(IEnumerable<ItemRecord> items);
    Task<ItemRecord?> GetItemByIdAsync(int id);
    Task<ItemRecord?> GetItemByNameAsync(string name);
    Task<IReadOnlyList<ItemRecord>> GetAllItemsAsync();
    Task<IReadOnlyList<ItemRecord>> SearchItemsAsync(string query);

    // NPCs
    Task CacheNpcsAsync(IEnumerable<NpcRecord> npcs);
    Task<NpcRecord?> GetNpcByIdAsync(int id);
    Task<NpcRecord?> GetNpcByNameAsync(string name);
    Task<IReadOnlyList<NpcRecord>> GetAllNpcsAsync();
    Task<IReadOnlyList<NpcRecord>> SearchNpcsAsync(string query);

    // Spells
    Task CacheSpellsAsync(IEnumerable<SpellRecord> spells);
    Task<SpellRecord?> GetSpellByIdAsync(int id);
    Task<SpellRecord?> GetSpellByNameAsync(string name);
    Task<IReadOnlyList<SpellRecord>> GetAllSpellsAsync();
    Task<IReadOnlyList<SpellRecord>> SearchSpellsAsync(string query);

    // Classes
    Task CacheClassesAsync(IEnumerable<ClassRecord> classes);
    Task<ClassRecord?> GetClassByIdAsync(int id);
    Task<ClassRecord?> GetClassByNameAsync(string name);
    Task<IReadOnlyList<ClassRecord>> GetAllClassesAsync();
    Task<IReadOnlyList<ClassRecord>> SearchClassesAsync(string query);
}

