using System.Collections.Concurrent;
using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Net;
using Acorn.Net.PacketHandlers.Player.Warp;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
namespace Acorn;

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
            var added = Maps.TryAdd(map.Id, new MapState(map));
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

public record GlobalMessage(
    Guid Id,
    string Message,
    string Author,
    DateTime CreatedAt
)
{
    public static GlobalMessage Welcome()
    {
        return new GlobalMessage(
            Guid.NewGuid(),
            "Welcome to Acorn! Please be respectful.",
            "Server",
            DateTime.UtcNow
        );
    }
}

public class MapState
{
    public MapState(MapWithId data)
    {
        Id = data.Id;
        Data = data.Map;
    }

    public int Id { get; set; }
    public Emf Data { get; set; }

    public ConcurrentBag<NpcState> Npcs { get; set; } = new();
    public ConcurrentBag<PlayerConnection> Players { get; set; } = new();

    public bool HasPlayer(PlayerConnection player)
    {
        return Players.Contains(player);
    }

    public IEnumerable<PlayerConnection> PlayersExcept(PlayerConnection playerConnection)
    {
        return Players.Where(x => x != playerConnection);
    }

    public async Task BroadcastPacket(IPacket packet, PlayerConnection? except = null)
    {
        var otherPlayers = Players.Where(x => except is null || x != except)
            .ToList();

        var broadcast = otherPlayers
            .Select(async otherPlayer => await otherPlayer.Send(packet));

        await Task.WhenAll(broadcast);
    }

    public NearbyInfo AsNearbyInfo(PlayerConnection? except = null, WarpEffect warpEffect = WarpEffect.None)
    {
        return new NearbyInfo
        {
            Characters = Players
                .Where(x => x.Character is not null)
                .Where(x => except == null || x != except)
                .Select(x => x.Character?.AsCharacterMapInfo(x.SessionId, warpEffect))
                .ToList(),
            Items = [],
            Npcs = AsNpcMapInfo()
        };
    }

    public List<NpcMapInfo> AsNpcMapInfo()
    {
        return Npcs.Select((x, i) => x.AsNpcMapInfo(i)).ToList();
    }

    public async Task Enter(PlayerConnection player, WarpEffect warpEffect = WarpEffect.None)
    {
        if (player.Character is null)
        {
            return;
        }

        player.Character.Map = Id;

        if (!Players.Contains(player))
        {
            Players.Add(player);
        }

        await BroadcastPacket(new PlayersAgreeServerPacket
        {
            Nearby = AsNearbyInfo(null, warpEffect)
        }, player);
    }

    public async Task Leave(PlayerConnection player, WarpEffect warpEffect = WarpEffect.None)
    {
        Players = new ConcurrentBag<PlayerConnection>(Players.Where(p => p != player));

        await BroadcastPacket(new PlayersRemoveServerPacket
        {
            PlayerId = player.SessionId
        });

        await BroadcastPacket(new AvatarRemoveServerPacket
        {
            PlayerId = player.SessionId,
            WarpEffect = warpEffect
        });
    }

    public void Tick()
    {
        foreach (var player in Players)
        {
            var before = player.Character?.Hp;
            var after = player.Character?.Recover(15);
        }
    }
}

public class NpcState
{
    public NpcState(EnfRecord data)
    {
        Data = data;
    }

    public EnfRecord Data { get; set; }
    public Direction Direction { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Id { get; set; }

    public NpcMapInfo AsNpcMapInfo(int index)
    {
        return new NpcMapInfo
        {
            Coords = new Coords { X = X, Y = Y },
            Direction = Direction,
            Id = Id,
            Index = index
        };
    }
}