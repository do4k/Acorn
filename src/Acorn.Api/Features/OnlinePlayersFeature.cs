using Acorn.Api.Options;
using Acorn.Shared.Caching;
using Acorn.Shared.Models;
using Acorn.Shared.Models.Online;
using Microsoft.Extensions.Options;

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

    private static async Task<IResult> GetOnlinePlayers(
        ICharacterCacheService characterCache,
        IOptions<AcornbotOptions> acornbotOptions)
    {
        var players = await characterCache.GetOnlinePlayersAsync();
        return Results.Ok(WithAcornbot(players, acornbotOptions.Value));
    }

    /// <summary>
    ///     Prepends the Acornbot pseudo-player (title = the discovery hint) while the
    ///     bot is enabled. The API's online cache is process-local today, so the bot
    ///     entry is composed here; the name check dedupes for the day the server's
    ///     seeded cache record becomes visible (e.g. via a shared Redis cache).
    /// </summary>
    internal static OnlinePlayersRecord WithAcornbot(OnlinePlayersRecord players, AcornbotOptions acornbot)
    {
        if (!acornbot.Enabled)
        {
            return players;
        }

        var botName = acornbot.Name.Trim().ToLowerInvariant();
        if (botName.Length == 0
            || players.Players.Any(p => p.Name.Trim().ToLowerInvariant() == botName))
        {
            return players;
        }

        var merged = new List<OnlinePlayerSummary>(players.Players.Count + 1)
        {
            new()
            {
                Name = acornbot.Name,
                Title = AcornbotPresence.OnlineListTitle,
                Level = 1
            }
        };
        merged.AddRange(players.Players);

        return new OnlinePlayersRecord
        {
            TotalOnline = players.TotalOnline + 1,
            Players = merged
        };
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
