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

    public async Task Tick()
    {
        List<Task> tasks = new();
        var random = new Random();

        var newPositions = Npcs.Select(npc =>
        {
            npc.Direction = (Direction)random.Next(0, 3);
            var nextCoords = npc.NextCoords();
            npc.X = nextCoords.X;
            npc.Y = nextCoords.Y;
            return npc;
        });
        
        tasks.Add(BroadcastPacket(new NpcPlayerServerPacket
        {
            Positions = newPositions.Select((x, id) => new NpcUpdatePosition
            {
                NpcIndex = id,
                Coords = new Coords
                {
                    X = x.X,
                    Y = x.Y
                },
                Direction = x.Direction
            }).ToList()
        }));
        
        foreach (var player in Players)
        {
            if (player.Character is null)
            {
                _logger.LogWarning("Player {PlayerId} has no character associated with them, skipping tick.", player.SessionId);
                continue;
            }
            
            var hp = player.Character.SitState switch
            {
                SitState.Stand => player.Character.Recover(5),
                _ => player.Character.Recover(10)
            };

            tasks.Add(player.Send(new RecoverPlayerServerPacket
            {
                Hp = hp,
                Tp = player.Character.Tp,
            }));
        }

        await Task.WhenAll(tasks);
    }
}