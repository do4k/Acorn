using Acorn.Shared.Models.Pub;

namespace Acorn.Shared.Caching;

/// <summary>
/// Redis-based pub file cache service.
/// </summary>
public class PubCacheService : IPubCacheService
{
    private readonly ICacheService _cache;

    // Cache key prefixes
    private const string ItemByIdKey = "pub:item:id:";
    private const string ItemByNameKey = "pub:item:name:";
    private const string ItemsAllKey = "pub:items:all";
    
    private const string NpcByIdKey = "pub:npc:id:";
    private const string NpcByNameKey = "pub:npc:name:";
    private const string NpcsAllKey = "pub:npcs:all";
    
    private const string SpellByIdKey = "pub:spell:id:";
    private const string SpellByNameKey = "pub:spell:name:";
    private const string SpellsAllKey = "pub:spells:all";
    
    private const string ClassByIdKey = "pub:class:id:";
    private const string ClassByNameKey = "pub:class:name:";
    private const string ClassesAllKey = "pub:classes:all";

    public PubCacheService(ICacheService cache)
    {
        _cache = cache;
    }

    #region Items

    public async Task CacheItemsAsync(IEnumerable<ItemRecord> items)
    {
        var itemList = items.ToList();
        
        // Cache the full list
        await _cache.SetAsync(ItemsAllKey, itemList);
        
        // Cache by ID and name for quick lookups
        foreach (var item in itemList)
        {
            await _cache.SetAsync($"{ItemByIdKey}{item.Id}", item);
            if (!string.IsNullOrEmpty(item.Name))
            {
                await _cache.SetAsync($"{ItemByNameKey}{item.Name.ToLowerInvariant()}", item);
            }
        }
    }

    public async Task<ItemRecord?> GetItemByIdAsync(int id)
    {
        return await _cache.GetAsync<ItemRecord>($"{ItemByIdKey}{id}");
    }

    public async Task<ItemRecord?> GetItemByNameAsync(string name)
    {
        return await _cache.GetAsync<ItemRecord>($"{ItemByNameKey}{name.ToLowerInvariant()}");
    }

    public async Task<IReadOnlyList<ItemRecord>> GetAllItemsAsync()
    {
        var items = await _cache.GetAsync<List<ItemRecord>>(ItemsAllKey);
        return items ?? [];
    }

    public async Task<IReadOnlyList<ItemRecord>> SearchItemsAsync(string query)
    {
        var all = await GetAllItemsAsync();
        return all.Where(i => i.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    #endregion

    #region NPCs

    public async Task CacheNpcsAsync(IEnumerable<NpcRecord> npcs)
    {
        var npcList = npcs.ToList();
        
        await _cache.SetAsync(NpcsAllKey, npcList);
        
        foreach (var npc in npcList)
        {
            await _cache.SetAsync($"{NpcByIdKey}{npc.Id}", npc);
            if (!string.IsNullOrEmpty(npc.Name))
            {
                await _cache.SetAsync($"{NpcByNameKey}{npc.Name.ToLowerInvariant()}", npc);
            }
        }
    }

    public async Task<NpcRecord?> GetNpcByIdAsync(int id)
    {
        return await _cache.GetAsync<NpcRecord>($"{NpcByIdKey}{id}");
    }

    public async Task<NpcRecord?> GetNpcByNameAsync(string name)
    {
        return await _cache.GetAsync<NpcRecord>($"{NpcByNameKey}{name.ToLowerInvariant()}");
    }

    public async Task<IReadOnlyList<NpcRecord>> GetAllNpcsAsync()
    {
        var npcs = await _cache.GetAsync<List<NpcRecord>>(NpcsAllKey);
        return npcs ?? [];
    }

    public async Task<IReadOnlyList<NpcRecord>> SearchNpcsAsync(string query)
    {
        var all = await GetAllNpcsAsync();
        return all.Where(n => n.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    #endregion

    #region Spells

    public async Task CacheSpellsAsync(IEnumerable<SpellRecord> spells)
    {
        var spellList = spells.ToList();
        
        await _cache.SetAsync(SpellsAllKey, spellList);
        
        foreach (var spell in spellList)
        {
            await _cache.SetAsync($"{SpellByIdKey}{spell.Id}", spell);
            if (!string.IsNullOrEmpty(spell.Name))
            {
                await _cache.SetAsync($"{SpellByNameKey}{spell.Name.ToLowerInvariant()}", spell);
            }
        }
    }

    public async Task<SpellRecord?> GetSpellByIdAsync(int id)
    {
        return await _cache.GetAsync<SpellRecord>($"{SpellByIdKey}{id}");
    }

    public async Task<SpellRecord?> GetSpellByNameAsync(string name)
    {
        return await _cache.GetAsync<SpellRecord>($"{SpellByNameKey}{name.ToLowerInvariant()}");
    }

    public async Task<IReadOnlyList<SpellRecord>> GetAllSpellsAsync()
    {
        var spells = await _cache.GetAsync<List<SpellRecord>>(SpellsAllKey);
        return spells ?? [];
    }

    public async Task<IReadOnlyList<SpellRecord>> SearchSpellsAsync(string query)
    {
        var all = await GetAllSpellsAsync();
        return all.Where(s => s.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    #endregion

    #region Classes

    public async Task CacheClassesAsync(IEnumerable<ClassRecord> classes)
    {
        var classList = classes.ToList();
        
        await _cache.SetAsync(ClassesAllKey, classList);
        
        foreach (var cls in classList)
        {
            await _cache.SetAsync($"{ClassByIdKey}{cls.Id}", cls);
            if (!string.IsNullOrEmpty(cls.Name))
            {
                await _cache.SetAsync($"{ClassByNameKey}{cls.Name.ToLowerInvariant()}", cls);
            }
        }
    }

    public async Task<ClassRecord?> GetClassByIdAsync(int id)
    {
        return await _cache.GetAsync<ClassRecord>($"{ClassByIdKey}{id}");
    }

    public async Task<ClassRecord?> GetClassByNameAsync(string name)
    {
        return await _cache.GetAsync<ClassRecord>($"{ClassByNameKey}{name.ToLowerInvariant()}");
    }

    public async Task<IReadOnlyList<ClassRecord>> GetAllClassesAsync()
    {
        var classes = await _cache.GetAsync<List<ClassRecord>>(ClassesAllKey);
        return classes ?? [];
    }

    public async Task<IReadOnlyList<ClassRecord>> SearchClassesAsync(string query)
    {
        var all = await GetAllClassesAsync();
        return all.Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    #endregion
}

