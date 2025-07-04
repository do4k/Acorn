using System.Collections.Concurrent;
using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Net;
using Acorn.World.Npc;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.World.Map;


public class MapState
{
    private readonly ILogger<MapState> _logger;
    private readonly WorldState _world;

    public MapState(MapWithId data, WorldState world, IDataFileRepository dataRepository, ILogger<MapState> logger)
    {
        Id = data.Id;
        Data = data.Map;
        _logger = logger;
        _world = world;

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
    public ConcurrentBag<PlayerState> Players { get; set; } = new();

    public bool HasPlayer(PlayerState player)
    {
        return Players.Contains(player);
    }

    public IEnumerable<PlayerState> PlayersExcept(PlayerState? except)
        => Players.Where(x => except is null || x != except);

    public async Task BroadcastPacket(IPacket packet, PlayerState? except = null)
    {
        var broadcast = PlayersExcept(except)
            .Select(async otherPlayer => await otherPlayer.Send(packet));

        await Task.WhenAll(broadcast);
    }

    public NearbyInfo AsNearbyInfo(PlayerState? except = null, WarpEffect warpEffect = WarpEffect.None)
        => new()
        {
            Characters = Players
                .Where(x => x.Character is not null)
                .Where(x => except == null || x != except)
                .Select(x => x.Character?.AsCharacterMapInfo(x.SessionId, warpEffect))
                .ToList(),
            Items = [],
            Npcs = AsNpcMapInfo()
        };

    public List<NpcMapInfo> AsNpcMapInfo()
        => Npcs.Select((x, i) => x.AsNpcMapInfo(i)).ToList();

    public async Task NotifyEnter(PlayerState player, WarpEffect warpEffect = WarpEffect.None)
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

        player.CurrentMap = this;
    }

    public async Task NotifyLeave(PlayerState player, WarpEffect warpEffect = WarpEffect.None)
    {
        Players = new ConcurrentBag<PlayerState>(Players.Where(p => p != player));

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
            or MapTileSpec.NpcBoundary
            => false,
            _ => true
        };

    public async Task Tick()
    {
        if (Players.Any() is false)
            return;

        List<Task> tasks = new();
        var newPositions = Npcs.Select(MoveNpc).ToList();
        var npcUpdates = newPositions
            .Select((x, id) => new
            {
                Position = new NpcUpdatePosition
                {
                    NpcIndex = id,
                    Coords = new Coords
                    {
                        X = x.Item1.X,
                        Y = x.Item1.Y
                    },
                    Direction = x.Item1.Direction
                },
                Moved = x.Item2
            }).ToList();

        tasks.Add(BroadcastPacket(new NpcPlayerServerPacket
        {
            Positions = npcUpdates.Where(x => x.Moved).Select(x => x.Position).ToList()
        }));

        tasks.AddRange(Players.Select(RecoverPlayer));

        await Task.WhenAll(tasks);
    }

    private Task RecoverPlayer(PlayerState player)
    {
        if (player.Character is null)
        {
            _logger.LogWarning("Player {PlayerId} has no character associated with them, skipping tick.", player.SessionId);
            return Task.CompletedTask;
        }

        var hp = player.Character.SitState switch
        {
            SitState.Stand => player.Character.Recover(5),
            _ => player.Character.Recover(10)
        };

        return player.Send(new RecoverPlayerServerPacket
        {
            Hp = hp,
            Tp = player.Character.Tp,
        });
    }

    private (NpcState, bool) MoveNpc(NpcState npc)
    {
        var newDirection = _world.NpcDirection;
        var nextCoords = npc.NextCoords(newDirection);
        if (nextCoords.X < 0 || nextCoords.Y < 0 || nextCoords.X > Data.Width || nextCoords.Y > Data.Height)
        {
            return (npc, false);
        }

        if (Players.Any(x => x.Character?.AsCoords().Equals(nextCoords) == true))
        {
            return (npc, false);
        }

        if (Npcs.Any(x => x.AsCoords().Equals(nextCoords)))
        {
            return (npc, false);
        }

        var row = Data.TileSpecRows.Where(x => x.Y == nextCoords.Y).ToList();
        var tile = row.SelectMany(x => x.Tiles)
            .FirstOrDefault(x => x.X == nextCoords.X);

        if (tile is not null)
        {
            if (IsNpcWalkable(tile.TileSpec) is false)
            {
                return (npc, false);
            }
        }

        npc.X = nextCoords.X;
        npc.Y = nextCoords.Y;
        npc.Direction = newDirection;
        return (npc, true);
    }
}