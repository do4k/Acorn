using System.Collections.Concurrent;
using Acorn.Database.Repository;
using Acorn.Net;
using Acorn.Net.PacketHandlers.Player.Warp;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.World;

public class WorldState
{
    public ConcurrentDictionary<Guid, GlobalMessage> GlobalMessages = [];
    public ConcurrentDictionary<int, MapState> Maps = [];
    public ConcurrentDictionary<int, PlayerConnection> Players = [];
    private readonly ILogger<WorldState> _logger;

    public WorldState(IDataFileRepository dataRepository, ILogger<WorldState> logger)
    {
        _logger = logger;
        foreach (var map in dataRepository.Maps)
        {
            var added = Maps.TryAdd(map.Id, new MapState(map, dataRepository, logger));
            if (added is false)
            {
                _logger.LogWarning("Failed to add map {MapId} to world state", map.Id);
            }
        }
    }

    public MapState? MapFor(PlayerConnection player)
    {
        var exists = Maps.TryGetValue(player.Character?.Map ?? -1, out var map);
        if (exists is true && map is not null)
        {
            return map;
        }

        _logger.LogWarning("Player {CharacterName} ({SessionId}) attempted to access non-existent map {MapId}",
            player.Character?.Name, player.SessionId, player.Character?.Map);
        return null;
    }

    public Task Refresh(PlayerConnection player)
    {
        return player.Character switch
        {
            null => throw new InvalidOperationException(
                "Cannot refresh player where the selected character is not initialised"),
            _ => Warp(player, player.Character.Map, player.Character.X, player.Character.Y)
        };
    }

    public async Task Warp(PlayerConnection player, int mapId, int x, int y, WarpEffect warpEffect = WarpEffect.None,
        bool localWarp = true)
    {
        player.WarpSession = new WarpSession
        {
            WarpEffect = warpEffect,
            Local = localWarp,
            MapId = mapId,
            X = x,
            Y = y
        };

        await player.WarpSession.Begin(player, this);

        if (!localWarp)
        {
            var map = MapFor(player);
            if (map is not null)
            {
                await map.Leave(player, warpEffect);
            }

            var found = Maps.TryGetValue(mapId, out var newMap);
            if (found is false || newMap is null)
            {
                player.Disconnect();
                _logger.LogWarning("Player {CharacterName} ({SessionId}) attempted to warp to non-existent map {MapId}", player.Character?.Name, player.SessionId, mapId);
                return;
            }
            await newMap.Enter(player, warpEffect);
        }
    }
}