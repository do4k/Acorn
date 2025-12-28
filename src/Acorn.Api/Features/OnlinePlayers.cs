using Acorn.Shared.Caching;
using Acorn.Shared.Models;
using Acorn.Shared.Models.Online;

namespace Acorn.Api.Features;

public static class OnlinePlayersFeature
{
    public static RouteGroupBuilder MapOnlinePlayersEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/online")
            .WithTags("Online Players");

        group.MapGet("/", GetOnlinePlayers)
            .WithName("GetOnlinePlayers")
            .WithDescription("Get summary of all online players")
            .Produces<OnlinePlayersRecord>()
            .Produces<ServiceUnavailableError>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/characters", GetAllOnlineCharacters)
            .WithName("GetAllOnlineCharacters")
            .WithDescription("Get detailed info for all online characters")
            .Produces<IReadOnlyList<OnlineCharacterRecord>>()
            .Produces<ServiceUnavailableError>(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/character/{name}", GetCharacterByName)
            .WithName("GetOnlineCharacterByName")
            .WithDescription("Get an online character by name")
            .Produces<OnlineCharacterRecord>()
            .Produces<NotFoundError>(StatusCodes.Status404NotFound)
            .Produces<ServiceUnavailableError>(StatusCodes.Status503ServiceUnavailable);

        return group;
    }

    private static async Task<IResult> GetOnlinePlayers(ICharacterCacheService characterCache, ICacheService cache)
    {
        if (cache is InMemoryCacheService)
            return RedisUnavailable();

        var players = await characterCache.GetOnlinePlayersAsync();
        return Results.Ok(players);
    }

    private static async Task<IResult> GetAllOnlineCharacters(ICharacterCacheService characterCache, ICacheService cache)
    {
        if (cache is InMemoryCacheService)
            return RedisUnavailable();

        var characters = await characterCache.GetAllOnlineCharactersAsync();
        return Results.Ok(characters);
    }

    private static async Task<IResult> GetCharacterByName(string name, ICharacterCacheService characterCache, ICacheService cache)
    {
        if (cache is InMemoryCacheService)
            return RedisUnavailable();

        var character = await characterCache.GetCharacterByNameAsync(name);
        if (character == null)
            return Results.NotFound(new NotFoundError
            {
                Error = "Character not found",
                Message = "The character is not online or does not exist",
                ResourceType = "Character",
                ResourceId = name
            });

        return Results.Ok(character);
    }

    private static IResult RedisUnavailable() => Results.Json(new ServiceUnavailableError
    { 
        Error = "Redis is not available",
        Message = "The API requires Redis to access game server data. In-memory cache cannot be shared between processes.",
        Service = "Redis"
    }, statusCode: StatusCodes.Status503ServiceUnavailable);
}

