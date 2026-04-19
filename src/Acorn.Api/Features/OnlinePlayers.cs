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
            .Produces<OnlinePlayersRecord>();

        group.MapGet("/characters", GetAllOnlineCharacters)
            .WithName("GetAllOnlineCharacters")
            .WithDescription("Get detailed info for all online characters")
            .Produces<IReadOnlyList<OnlineCharacterRecord>>();

        group.MapGet("/character/{name}", GetCharacterByName)
            .WithName("GetOnlineCharacterByName")
            .WithDescription("Get an online character by name")
            .Produces<OnlineCharacterRecord>()
            .Produces<NotFoundError>(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> GetOnlinePlayers(ICharacterCacheService characterCache)
    {
        var players = await characterCache.GetOnlinePlayersAsync();
        return Results.Ok(players);
    }

    private static async Task<IResult> GetAllOnlineCharacters(ICharacterCacheService characterCache)
    {
        var characters = await characterCache.GetAllOnlineCharactersAsync();
        return Results.Ok(characters);
    }

    private static async Task<IResult> GetCharacterByName(string name, ICharacterCacheService characterCache)
    {
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
}
