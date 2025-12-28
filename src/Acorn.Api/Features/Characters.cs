using Acorn.Shared.Caching;
using Acorn.Shared.Models;

namespace Acorn.Api.Features;

public static class CharactersFeature
{
    public static RouteGroupBuilder MapCharacterEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/characters")
            .WithTags("Characters");

        group.MapGet("/{name}", GetCharacter)
            .WithName("GetCharacter")
            .WithDescription("Get character details by name from cache")
            .Produces<object>()
            .Produces<NotFoundError>(StatusCodes.Status404NotFound)
            .Produces<ServiceUnavailableError>(StatusCodes.Status503ServiceUnavailable);

        return group;
    }

    private static async Task<IResult> GetCharacter(string name, ICacheService cache)
    {
        if (cache is InMemoryCacheService)
            return RedisUnavailable();

        var character = await cache.GetAsync<object>($"character:name:{name.ToLower()}");
        if (character == null)
            return Results.NotFound(new NotFoundError
            {
                Error = "Character not found",
                Message = "The character does not exist or is not cached",
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

