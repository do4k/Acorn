using Acorn.Shared.Caching;
using Acorn.Shared.Models;
using Acorn.Shared.Models.Pub;

namespace Acorn.Api.Features;

public static class PubFeature
{
    public static WebApplication MapPubEndpoints(this WebApplication app)
    {
        app.MapItemEndpoints();
        app.MapNpcEndpoints();
        app.MapSpellEndpoints();
        app.MapClassEndpoints();
        return app;
    }

    private static void MapItemEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/pub/items")
            .WithTags("Pub - Items");

        group.MapGet("/", GetAllItems)
            .WithName("GetAllItems")
            .WithDescription("Get all items from pub files")
            .Produces<IReadOnlyList<ItemRecord>>()
            .Produces<ServiceUnavailableError>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/{id:int}", GetItemById)
            .WithName("GetItemById")
            .WithDescription("Get an item by its ID")
            .Produces<ItemRecord>()
            .Produces<NotFoundError>(StatusCodes.Status404NotFound)
            .Produces<ServiceUnavailableError>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/name/{name}", GetItemByName)
            .WithName("GetItemByName")
            .WithDescription("Get an item by its exact name")
            .Produces<ItemRecord>()
            .Produces<NotFoundError>(StatusCodes.Status404NotFound)
            .Produces<ServiceUnavailableError>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/search/{query}", SearchItems)
            .WithName("SearchItems")
            .WithDescription("Search items by partial name match")
            .Produces<IReadOnlyList<ItemRecord>>()
            .Produces<ServiceUnavailableError>(StatusCodes.Status503ServiceUnavailable);
    }

    private static void MapNpcEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/pub/npcs")
            .WithTags("Pub - NPCs");

        group.MapGet("/", GetAllNpcs)
            .WithName("GetAllNpcs")
            .WithDescription("Get all NPCs from pub files")
            .Produces<IReadOnlyList<NpcRecord>>()
            .Produces<ServiceUnavailableError>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/{id:int}", GetNpcById)
            .WithName("GetNpcById")
            .WithDescription("Get an NPC by its ID")
            .Produces<NpcRecord>()
            .Produces<NotFoundError>(StatusCodes.Status404NotFound)
            .Produces<ServiceUnavailableError>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/name/{name}", GetNpcByName)
            .WithName("GetNpcByName")
            .WithDescription("Get an NPC by its exact name")
            .Produces<NpcRecord>()
            .Produces<NotFoundError>(StatusCodes.Status404NotFound)
            .Produces<ServiceUnavailableError>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/search/{query}", SearchNpcs)
            .WithName("SearchNpcs")
            .WithDescription("Search NPCs by partial name match")
            .Produces<IReadOnlyList<NpcRecord>>()
            .Produces<ServiceUnavailableError>(StatusCodes.Status503ServiceUnavailable);
    }

    private static void MapSpellEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/pub/spells")
            .WithTags("Pub - Spells");

        group.MapGet("/", GetAllSpells)
            .WithName("GetAllSpells")
            .WithDescription("Get all spells from pub files")
            .Produces<IReadOnlyList<SpellRecord>>()
            .Produces<ServiceUnavailableError>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/{id:int}", GetSpellById)
            .WithName("GetSpellById")
            .WithDescription("Get a spell by its ID")
            .Produces<SpellRecord>()
            .Produces<NotFoundError>(StatusCodes.Status404NotFound)
            .Produces<ServiceUnavailableError>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/name/{name}", GetSpellByName)
            .WithName("GetSpellByName")
            .WithDescription("Get a spell by its exact name")
            .Produces<SpellRecord>()
            .Produces<NotFoundError>(StatusCodes.Status404NotFound)
            .Produces<ServiceUnavailableError>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/search/{query}", SearchSpells)
            .WithName("SearchSpells")
            .WithDescription("Search spells by partial name match")
            .Produces<IReadOnlyList<SpellRecord>>()
            .Produces<ServiceUnavailableError>(StatusCodes.Status503ServiceUnavailable);
    }

    private static void MapClassEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/pub/classes")
            .WithTags("Pub - Classes");

        group.MapGet("/", GetAllClasses)
            .WithName("GetAllClasses")
            .WithDescription("Get all classes from pub files")
            .Produces<IReadOnlyList<ClassRecord>>()
            .Produces<ServiceUnavailableError>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/{id:int}", GetClassById)
            .WithName("GetClassById")
            .WithDescription("Get a class by its ID")
            .Produces<ClassRecord>()
            .Produces<NotFoundError>(StatusCodes.Status404NotFound)
            .Produces<ServiceUnavailableError>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/name/{name}", GetClassByName)
            .WithName("GetClassByName")
            .WithDescription("Get a class by its exact name")
            .Produces<ClassRecord>()
            .Produces<NotFoundError>(StatusCodes.Status404NotFound)
            .Produces<ServiceUnavailableError>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/search/{query}", SearchClasses)
            .WithName("SearchClasses")
            .WithDescription("Search classes by partial name match")
            .Produces<IReadOnlyList<ClassRecord>>()
            .Produces<ServiceUnavailableError>(StatusCodes.Status503ServiceUnavailable);
    }

    #region Item Handlers

    private static async Task<IResult> GetAllItems(IPubCacheService pubCache, ICacheService cache)
    {
        if (cache is InMemoryCacheService)
            return RedisUnavailable();

        var items = await pubCache.GetAllItemsAsync();
        return Results.Ok(items);
    }

    private static async Task<IResult> GetItemById(int id, IPubCacheService pubCache, ICacheService cache)
    {
        if (cache is InMemoryCacheService)
            return RedisUnavailable();

        var item = await pubCache.GetItemByIdAsync(id);
        return item is null
            ? Results.NotFound(new NotFoundError { Error = "Item not found", ResourceType = "Item", ResourceId = id.ToString() })
            : Results.Ok(item);
    }

    private static async Task<IResult> GetItemByName(string name, IPubCacheService pubCache, ICacheService cache)
    {
        if (cache is InMemoryCacheService)
            return RedisUnavailable();

        var item = await pubCache.GetItemByNameAsync(name);
        return item is null
            ? Results.NotFound(new NotFoundError { Error = "Item not found", ResourceType = "Item", ResourceId = name })
            : Results.Ok(item);
    }

    private static async Task<IResult> SearchItems(string query, IPubCacheService pubCache, ICacheService cache)
    {
        if (cache is InMemoryCacheService)
            return RedisUnavailable();

        var items = await pubCache.SearchItemsAsync(query);
        return Results.Ok(items);
    }

    #endregion

    #region NPC Handlers

    private static async Task<IResult> GetAllNpcs(IPubCacheService pubCache, ICacheService cache)
    {
        if (cache is InMemoryCacheService)
            return RedisUnavailable();

        var npcs = await pubCache.GetAllNpcsAsync();
        return Results.Ok(npcs);
    }

    private static async Task<IResult> GetNpcById(int id, IPubCacheService pubCache, ICacheService cache)
    {
        if (cache is InMemoryCacheService)
            return RedisUnavailable();

        var npc = await pubCache.GetNpcByIdAsync(id);
        return npc is null
            ? Results.NotFound(new NotFoundError { Error = "NPC not found", ResourceType = "NPC", ResourceId = id.ToString() })
            : Results.Ok(npc);
    }

    private static async Task<IResult> GetNpcByName(string name, IPubCacheService pubCache, ICacheService cache)
    {
        if (cache is InMemoryCacheService)
            return RedisUnavailable();

        var npc = await pubCache.GetNpcByNameAsync(name);
        return npc is null
            ? Results.NotFound(new NotFoundError { Error = "NPC not found", ResourceType = "NPC", ResourceId = name })
            : Results.Ok(npc);
    }

    private static async Task<IResult> SearchNpcs(string query, IPubCacheService pubCache, ICacheService cache)
    {
        if (cache is InMemoryCacheService)
            return RedisUnavailable();

        var npcs = await pubCache.SearchNpcsAsync(query);
        return Results.Ok(npcs);
    }

    #endregion

    #region Spell Handlers

    private static async Task<IResult> GetAllSpells(IPubCacheService pubCache, ICacheService cache)
    {
        if (cache is InMemoryCacheService)
            return RedisUnavailable();

        var spells = await pubCache.GetAllSpellsAsync();
        return Results.Ok(spells);
    }

    private static async Task<IResult> GetSpellById(int id, IPubCacheService pubCache, ICacheService cache)
    {
        if (cache is InMemoryCacheService)
            return RedisUnavailable();

        var spell = await pubCache.GetSpellByIdAsync(id);
        return spell is null
            ? Results.NotFound(new NotFoundError { Error = "Spell not found", ResourceType = "Spell", ResourceId = id.ToString() })
            : Results.Ok(spell);
    }

    private static async Task<IResult> GetSpellByName(string name, IPubCacheService pubCache, ICacheService cache)
    {
        if (cache is InMemoryCacheService)
            return RedisUnavailable();

        var spell = await pubCache.GetSpellByNameAsync(name);
        return spell is null
            ? Results.NotFound(new NotFoundError { Error = "Spell not found", ResourceType = "Spell", ResourceId = name })
            : Results.Ok(spell);
    }

    private static async Task<IResult> SearchSpells(string query, IPubCacheService pubCache, ICacheService cache)
    {
        if (cache is InMemoryCacheService)
            return RedisUnavailable();

        var spells = await pubCache.SearchSpellsAsync(query);
        return Results.Ok(spells);
    }

    #endregion

    #region Class Handlers

    private static async Task<IResult> GetAllClasses(IPubCacheService pubCache, ICacheService cache)
    {
        if (cache is InMemoryCacheService)
            return RedisUnavailable();

        var classes = await pubCache.GetAllClassesAsync();
        return Results.Ok(classes);
    }

    private static async Task<IResult> GetClassById(int id, IPubCacheService pubCache, ICacheService cache)
    {
        if (cache is InMemoryCacheService)
            return RedisUnavailable();

        var cls = await pubCache.GetClassByIdAsync(id);
        return cls is null
            ? Results.NotFound(new NotFoundError { Error = "Class not found", ResourceType = "Class", ResourceId = id.ToString() })
            : Results.Ok(cls);
    }

    private static async Task<IResult> GetClassByName(string name, IPubCacheService pubCache, ICacheService cache)
    {
        if (cache is InMemoryCacheService)
            return RedisUnavailable();

        var cls = await pubCache.GetClassByNameAsync(name);
        return cls is null
            ? Results.NotFound(new NotFoundError { Error = "Class not found", ResourceType = "Class", ResourceId = name })
            : Results.Ok(cls);
    }

    private static async Task<IResult> SearchClasses(string query, IPubCacheService pubCache, ICacheService cache)
    {
        if (cache is InMemoryCacheService)
            return RedisUnavailable();

        var classes = await pubCache.SearchClassesAsync(query);
        return Results.Ok(classes);
    }

    #endregion

    private static IResult RedisUnavailable() => Results.Json(new ServiceUnavailableError
    {
        Error = "Redis is not available",
        Message = "The API requires Redis to access game server data. In-memory cache cannot be shared between processes.",
        Service = "Redis"
    }, statusCode: StatusCodes.Status503ServiceUnavailable);
}

