using Acorn.Extensions;
using Acorn.Game.Services;
using Acorn.Infrastructure.Telemetry;
using Acorn.Net.Models;
using Acorn.Options;
using Acorn.World;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using SdkSpell = Moffat.EndlessOnline.SDK.Protocol.Net.Spell;

namespace Acorn.Net.PacketHandlers.Player;

internal class WelcomeMsgClientPacketHandler : IPacketHandler<WelcomeMsgClientPacket>
{
    /// <summary>
    ///     Maximum number of news lines the client accepts after the motd line.
    /// </summary>
    internal const int MaxNewsLines = 8;

    private readonly string[] _newsTxt;
    private readonly IWorldQueries _world;
    private readonly IWeightCalculator _weightCalculator;
    private readonly ServerOptions _serverOptions;
    private readonly ILogger<WelcomeMsgClientPacketHandler> _logger;

    public WelcomeMsgClientPacketHandler(
        IWorldQueries worldState,
        IWeightCalculator weightCalculator,
        IOptions<ServerOptions> serverOptions,
        IOptions<DataOptions> dataOptions,
        ILogger<WelcomeMsgClientPacketHandler> logger
    )
    {
        _world = worldState;
        _weightCalculator = weightCalculator;
        _serverOptions = serverOptions.Value;
        _logger = logger;
        _newsTxt = LoadNews(dataOptions.Value.NewsFile, logger);
    }

    /// <summary>
    ///     Reads the news file, returning an empty array if it is missing or unreadable.
    ///     Never throws so a bad/missing news file cannot drop the connection.
    /// </summary>
    internal static string[] LoadNews(string path, ILogger? logger = null)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllLines(path) : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger?.LogWarning(ex, "Could not read news file {NewsFile}", path);
            return [];
        }
    }

    /// <summary>
    ///     Builds the fixed-size news list sent in the EnterGame reply. The first entry is
    ///     the motd spacer and is followed by at most <paramref name="maxNewsLines" /> news
    ///     lines padded with empty strings. Clamps instead of throwing when the file has
    ///     more lines than the client supports.
    /// </summary>
    internal static List<string> BuildNews(IReadOnlyList<string> newsTxt, int maxNewsLines = MaxNewsLines)
    {
        var news = new List<string> { " " };
        for (var i = 0; i < maxNewsLines; i++)
        {
            news.Add(i < newsTxt.Count ? newsTxt[i] : "");
        }

        return news;
    }

    public async Task HandleAsync(
        PlayerState playerState,
        WelcomeMsgClientPacket packet)
    {
        var character = playerState.Character;
        if (character is null)
        {
            _logger.LogWarning("Player {SessionId} entered the game without a character", playerState.SessionId);
            playerState.Disconnect();
            return;
        }

        var map = _world.FindMap(character.Map);
        if (map is null)
        {
            // Match eoserv: fall back to the spawn map, otherwise disconnect.
            var spawnMap = _world.FindMap(_serverOptions.NewCharacter.Map);
            if (spawnMap is null)
            {
                _logger.LogWarning(
                    "Player {Name} entered non-existent map {MapId} and spawn map {SpawnMapId} was not found - disconnecting",
                    character.Name, character.Map, _serverOptions.NewCharacter.Map);
                playerState.Disconnect();
                return;
            }

            _logger.LogWarning(
                "Player {Name} entered non-existent map {MapId} - warping to spawn map {SpawnMapId}",
                character.Name, character.Map, spawnMap.Id);
            character.Map = spawnMap.Id;
            character.X = _serverOptions.NewCharacter.X;
            character.Y = _serverOptions.NewCharacter.Y;
            map = spawnMap;
        }

        playerState.ClientState = ClientState.InGame;
        await map.NotifyEnter(playerState);
        _logger.PlayerEnteredWorld(character.Name ?? "unknown", playerState.SessionId, map.Id);

        var spells = character.Spells.Items
            .Select(s => new SdkSpell { Id = s.Id, Level = s.Level })
            .ToList();

        await playerState.Send(new WelcomeReplyServerPacket
        {
            WelcomeCode = WelcomeCode.EnterGame,
            WelcomeCodeData = new WelcomeReplyServerPacket.WelcomeCodeDataEnterGame
            {
                Items = character.Items().ToList(),
                News = BuildNews(_newsTxt),
                Weight = new Weight
                {
                    Current = _weightCalculator.GetCurrentWeight(character, _world.DataRepository.Eif),
                    Max = character.MaxWeight
                },
                Spells = spells,
                Nearby = map.AsNearbyInfo()
            }
        });
    }

}
