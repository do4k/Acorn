using System.Collections.Concurrent;
using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Net;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.World;


public class MapState
{
    private readonly ILogger<WorldState> _logger;
    private readonly Random _rnd = new();

    public MapState(MapWithId data, IDataFileRepository dataRepository, ILogger<WorldState> logger)
    {
        Id = data.Id;
        Data = data.Map;
        _logger = logger;
        var mapNpcs = data.Map.Npcs.SelectMany(mapNpc => Enumerable.Range(0, mapNpc.Amount).Select(_ => mapNpc));
        foreach (var npc in mapNpcs)
        {
            var npcData = dataRepository.Enf.GetNpc(npc.Id);
            if (npcData is null)
            {
                logger.LogError("Could not find npc with id {NpcId}", npc.Id);
                continue;
            }
            var npcState = new NpcState(npcData)
            {
                Direction = Direction.Down,
                X = npc.Coords.X,
                Y = npc.Coords.Y,
                Hp = npcData!.Hp,
                Id = npc.Id
            };
            Npcs.Add(npcState);
        }
    }

    public int Id { get; set; }
    public Emf Data { get; set; }

    public ConcurrentBag<NpcState> Npcs { get; set; } = new();
    public ConcurrentBag<ConnectionHandler> Players { get; set; } = new();

    public bool HasPlayer(ConnectionHandler player)
    {
        return Players.Contains(player);
    }

    public IEnumerable<ConnectionHandler> PlayersExcept(ConnectionHandler? except)
        => Players.Where(x => except is null || x != except);

    public async Task BroadcastPacket(IPacket packet, ConnectionHandler? except = null)
    {
        var broadcast = PlayersExcept(except)
            .Select(async otherPlayer => await otherPlayer.Send(packet));

        await Task.WhenAll(broadcast);
    }

    public NearbyInfo AsNearbyInfo(ConnectionHandler? except = null, WarpEffect warpEffect = WarpEffect.None)
        => new()
        {
            Characters = Players
                .Where(x => x.CharacterController is not null)
                .Where(x => except == null || x != except)
                .Select(x => x.CharacterController?.AsCharacterMapInfo(x.SessionId, warpEffect))
                .ToList(),
            Items = [],
            Npcs = AsNpcMapInfo()
        };

    public List<NpcMapInfo> AsNpcMapInfo()
        => Npcs.Select((x, i) => x.AsNpcMapInfo(i)).ToList();

    public async Task NotifyEnter(ConnectionHandler player, WarpEffect warpEffect = WarpEffect.None)
    {
        if (player.CharacterController is null)
        {
            return;
        }

        player.CharacterController.Data.Map = Id;

        if (!Players.Contains(player))
        {
            Players.Add(player);
        }

        await BroadcastPacket(new PlayersAgreeServerPacket
        {
            Nearby = AsNearbyInfo(null, warpEffect)
        }, player);

        player.CurrentMap = this;
    }

    public async Task NotifyLeave(ConnectionHandler player, WarpEffect warpEffect = WarpEffect.None)
    {
        Players = new ConcurrentBag<ConnectionHandler>(Players.Where(p => p != player));

        var playerRemoveTask = BroadcastPacket(new PlayersRemoveServerPacket
        {
            PlayerId = player.SessionId
        });

        var avatarRemoveTask = BroadcastPacket(new AvatarRemoveServerPacket
        {
            PlayerId = player.SessionId,
            WarpEffect = warpEffect
        });

        await Task.WhenAll(playerRemoveTask, avatarRemoveTask);
    }

    public bool IsNpcWalkable(MapTileSpec tileSpec)
        => tileSpec switch
        {
            MapTileSpec.Wall 
            or MapTileSpec.ChairDown  
            or MapTileSpec.ChairLeft  
            or MapTileSpec.ChairRight 
            or MapTileSpec.ChairUp 
            or MapTileSpec.ChairDownRight  
            or MapTileSpec.ChairUpLeft 
            or MapTileSpec.ChairAll 
            or MapTileSpec.Chest  
            or MapTileSpec.BankVault  
            or MapTileSpec.Edge  
            or MapTileSpec.Board1 
            or MapTileSpec.Board2
            or MapTileSpec.Board3
            or MapTileSpec.Board4
            or MapTileSpec.Board5
            or MapTileSpec.Board6
            or MapTileSpec.Board7
            or MapTileSpec.Board8
            or MapTileSpec.Jukebox
            => false,
            _ => true
        };

    public async Task Tick()
    {
        
        if (Players.Any() is false) 
            return;
        
        List<Task> tasks = new();

        var newPositions = Npcs.Select(npc =>
        {
            if (_rnd.Next(0, 100) < 90)
            {
                return npc;
            }
            
            npc.Direction = (Direction)_rnd.Next(0, 4);
            var nextCoords = npc.NextCoords();
            var row = Data.TileSpecRows.Where(x => x.Y == nextCoords.Y).ToList();
            if (row.Count == 0)
            {
                return npc;
            }
            
            var tile = row.SelectMany(x => x.Tiles)
                .FirstOrDefault(x => x.X == nextCoords.X);

            if (tile is not null && IsNpcWalkable(tile.TileSpec) is false)
            {
                return npc;
            }
            
            npc.X = nextCoords.X;
            npc.Y = nextCoords.Y;
            return npc;
        });
        
        var npcUpdates = newPositions.Select((x, id) => new NpcUpdatePosition
        {
            NpcIndex = id,
            Coords = new Coords
            {
                X = x.X,
                Y = x.Y
            },
            Direction = x.Direction
        }).ToList();
        
        tasks.Add(BroadcastPacket(new NpcPlayerServerPacket
        {
            Positions = npcUpdates
        }));
        
        foreach (var player in Players)
        {
            if (player.CharacterController is null)
            {
                _logger.LogWarning("ConnectionHandler {PlayerId} has no character associated with them, skipping tick.", player.SessionId);
                continue;
            }
            
            var hp = player.CharacterController.Data.SitState switch
            {
                SitState.Stand => player.CharacterController.Recover(5),
                _ => player.CharacterController.Recover(10)
            };

            tasks.Add(player.Send(new RecoverPlayerServerPacket
            {
                Hp = hp,
                Tp = player.CharacterController.Data.Tp,
            }));
        }

        await Task.WhenAll(tasks);
    }
}