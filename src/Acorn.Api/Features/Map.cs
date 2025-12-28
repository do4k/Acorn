using Acorn.Shared.Caching;
using Acorn.Shared.Models;
using Acorn.Shared.Models.Maps;

namespace Acorn.Api.Features;

public static class MapFeature
{
    public static RouteGroupBuilder MapMapEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/maps")
            .WithTags("Maps");

        group.MapGet("/", GetAllMaps)
            .WithName("GetAllMaps")
            .WithDescription("Get summary of all maps")
            .Produces<IReadOnlyList<MapSummary>>()
            .Produces<ServiceUnavailableError>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/{mapId:int}", GetMapState)
            .WithName("GetMapState")
            .WithDescription("Get the current state of a map including NPCs and players")
            .Produces<MapStateRecord>()
            .Produces<NotFoundError>(StatusCodes.Status404NotFound)
            .Produces<ServiceUnavailableError>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/{mapId:int}/players", GetMapPlayers)
            .WithName("GetMapPlayers")
            .WithDescription("Get players on a specific map")
            .Produces<IReadOnlyList<MapPlayerRecord>>()
            .Produces<NotFoundError>(StatusCodes.Status404NotFound)
            .Produces<ServiceUnavailableError>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/{mapId:int}/npcs", GetMapNpcs)
            .WithName("GetMapNpcs")
            .WithDescription("Get NPCs on a specific map")
            .Produces<IReadOnlyList<MapNpcRecord>>()
            .Produces<NotFoundError>(StatusCodes.Status404NotFound)
            .Produces<ServiceUnavailableError>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/{mapId:int}/items", GetMapItems)
            .WithName("GetMapItems")
            .WithDescription("Get items on a specific map")
            .Produces<IReadOnlyList<MapItemRecord>>()
            .Produces<NotFoundError>(StatusCodes.Status404NotFound)
            .Produces<ServiceUnavailableError>(StatusCodes.Status503ServiceUnavailable);

        return group;
    }

    private static async Task<IResult> GetAllMaps(IMapCacheService mapCache, ICacheService cache)
    {
        if (cache is InMemoryCacheService)
            return RedisUnavailable();

        var summaries = await mapCache.GetMapSummariesAsync();
        return Results.Ok(summaries);
    }

    private static async Task<IResult> GetMapState(int mapId, IMapCacheService mapCache, ICacheService cache)
    {
        if (cache is InMemoryCacheService)
            return RedisUnavailable();

        var state = await mapCache.GetMapStateAsync(mapId);
        if (state == null)
            return MapNotFound(mapId);

        return Results.Ok(state);
    }

    private static async Task<IResult> GetMapPlayers(int mapId, IMapCacheService mapCache, ICacheService cache)
    {
        if (cache is InMemoryCacheService)
            return RedisUnavailable();

        var state = await mapCache.GetMapStateAsync(mapId);
        if (state == null)
            return MapNotFound(mapId);

        return Results.Ok(state.Players);
    }

    private static async Task<IResult> GetMapNpcs(int mapId, IMapCacheService mapCache, ICacheService cache)
    {
        if (cache is InMemoryCacheService)
            return RedisUnavailable();

        var state = await mapCache.GetMapStateAsync(mapId);
        if (state == null)
            return MapNotFound(mapId);

        return Results.Ok(state.Npcs);
    }

    private static async Task<IResult> GetMapItems(int mapId, IMapCacheService mapCache, ICacheService cache)
    {
        if (cache is InMemoryCacheService)
            return RedisUnavailable();

        var state = await mapCache.GetMapStateAsync(mapId);
        if (state == null)
            return MapNotFound(mapId);

        return Results.Ok(state.Items);
    }

    private static IResult MapNotFound(int mapId) => Results.NotFound(new NotFoundError
    {
        Error = "Map not found",
        Message = "The map state is not cached or does not exist",
        ResourceType = "Map",
        ResourceId = mapId.ToString()
    });

    private static IResult RedisUnavailable() => Results.Json(new ServiceUnavailableError
    {
        Error = "Redis is not available",
        Message = "The API requires Redis to access game server data. In-memory cache cannot be shared between processes.",
        Service = "Redis"
    }, statusCode: StatusCodes.Status503ServiceUnavailable);
}


