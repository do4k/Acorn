using Acorn.Database.Models;
using Acorn.Database.Repository;
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
            .WithDescription("Get character details by name - checks cache first, then database")
            .Produces<object>()
            .Produces<NotFoundError>(StatusCodes.Status404NotFound)
            .Produces<ServiceUnavailableError>(StatusCodes.Status503ServiceUnavailable);

        return group;
    }

    private static async Task<IResult> GetCharacter(
        string name,
        ICacheService cache,
        IDbRepository<Character> characterRepository)
    {
        // Try cache first (online players)
        var character = await cache.GetAsync<object>($"character:name:{name.ToLower()}");
        if (character != null)
            return Results.Ok(character);

        // If not in cache, check if Redis is unavailable vs character just offline
        if (cache is InMemoryCacheService)
            return RedisUnavailable();

        // Fall back to database for offline characters
        try
        {
            var dbCharacter = await characterRepository.GetByKeyAsync(name);
            if (dbCharacter == null)
                return Results.NotFound(new NotFoundError
                {
                    Error = "Character not found",
                    Message = "The character does not exist",
                    ResourceType = "Character",
                    ResourceId = name
                });

            return Results.Ok(new
            {
                dbCharacter.Name,
                dbCharacter.Level,
                dbCharacter.Class,
                dbCharacter.Gender,
                dbCharacter.Map,
                dbCharacter.X,
                dbCharacter.Y,
                Status = "offline"
            });
        }
        catch
        {
            return Results.NotFound(new NotFoundError
            {
                Error = "Character not found",
                Message = "The character does not exist",
                ResourceType = "Character",
                ResourceId = name
            });
        }
    }

    private static IResult RedisUnavailable() => Results.Json(new ServiceUnavailableError
    {
        Error = "Redis is not available",
        Message = "The API requires Redis to access game server data. In-memory cache cannot be shared between processes.",
        Service = "Redis"
    }, statusCode: StatusCodes.Status503ServiceUnavailable);
}

