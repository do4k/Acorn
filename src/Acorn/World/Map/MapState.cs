using System.Collections.Concurrent;
using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Game.Services;
using Acorn.Net;
using Acorn.World.Npc;
using Acorn.World.Services;
using Acorn.World.Services.Map;
using Acorn.World.Services.Npc;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.World.Map;

/// <summary>
/// Represents an item on the map with protection timer
/// </summary>
public class MapItem
{
    public required int Id { get; set; }
    public required int Amount { get; set; }
    public required Coords Coords { get; set; }
    public int OwnerId { get; set; } // Player ID who dropped it
    public int ProtectedTicks { get; set; } // Ticks until anyone can pick up
}

public class MapState
{
    private readonly ILogger<MapState> _logger;
    private readonly IDataFileRepository _dataRepository;
    private readonly IMapBroadcastService _broadcastService;
    private readonly IMapController _mapController;
    private readonly INpcController _npcController;
    private readonly IPaperdollService _paperdollService;
    private readonly int _playerRecoverRate;

    // Tick counters for periodic events
    private int _playerRecoverTicks;

    public MapState(
        MapWithId data,
        IDataFileRepository dataRepository,
        IMapBroadcastService broadcastService,
        IMapController mapController,
        INpcController npcController,
        IPaperdollService paperdollService,
        int playerRecoverRate,
        ILogger<MapState> logger)
    {
        Id = data.Id;
        Data = data.Map;
        _logger = logger;
        _dataRepository = dataRepository;
        _broadcastService = broadcastService;
        _mapController = mapController;
        _npcController = npcController;
        _paperdollService = paperdollService;
        _playerRecoverRate = playerRecoverRate;

        var mapNpcs = data.Map.Npcs.SelectMany(mapNpc => Enumerable.Range(0, mapNpc.Amount).Select(_ => mapNpc));
        foreach (var npc in mapNpcs)
        {
            var npcData = dataRepository.Enf.GetNpc(npc.Id);
            if (npcData is null)
            {
                logger.LogError("Could not find npc with id {NpcId}", npc.Id);
                continue;
            }

            // SpawnType 7 means fixed position/direction (e.g., shopkeepers)
            // For type 7, the lower 2 bits of SpawnTime encode the direction
            var direction = npc.SpawnType == 7
                ? (Direction)(npc.SpawnTime & 0x03)
                : Direction.Down;

            var npcState = new NpcState(npcData)
            {
                Direction = direction,
                X = npc.Coords.X,
                Y = npc.Coords.Y,
                SpawnX = npc.Coords.X,
                SpawnY = npc.Coords.Y,
                Hp = npcData!.Hp,
                Id = npc.Id,
                SpawnType = npc.SpawnType,
                SpawnTime = npc.SpawnTime,
                // Fixed NPCs (SpawnType 7) are stationary, others wander/chase
                BehaviorType = npc.SpawnType == 7 ? NpcBehaviorType.Stationary : NpcBehaviorType.Wander
            };

            Npcs.Add(npcState);
        }
    }

    public int Id { get; set; }
    public Emf Data { get; set; }

    public ConcurrentBag<NpcState> Npcs { get; set; } = new();
    public ConcurrentBag<PlayerState> Players { get; set; } = new();
    public ConcurrentDictionary<int, MapItem> Items { get; set; } = new();

    public bool HasPlayer(PlayerState player)
    {
        return Players.Contains(player);
    }

    public IEnumerable<PlayerState> PlayersExcept(PlayerState? except)
        => Players.Where(x => except is null || x != except);

    public async Task BroadcastPacket(IPacket packet, PlayerState? except = null)
    {
        await _broadcastService.BroadcastPacket(Players, packet, except);
    }

    public NearbyInfo AsNearbyInfo(PlayerState? except = null, WarpEffect warpEffect = WarpEffect.None)
        => new()
        {
            Characters = Players
                .Where(x => x.Character is not null)
                .Where(x => except == null || x != except)
                .Select(x => x.Character?.AsCharacterMapInfo(x.SessionId, warpEffect, _paperdollService))
                .ToList(),
            Items = Items.Select(kvp => new ItemMapInfo
            {
                Uid = kvp.Key,
                Id = kvp.Value.Id,
                Coords = kvp.Value.Coords,
                Amount = kvp.Value.Amount
            }).ToList(),
            Npcs = AsNpcMapInfo()
        };

    public List<NpcMapInfo> AsNpcMapInfo()
        => Npcs.Select((x, i) => (npc: x, index: i))
            .Where(t => !t.npc.IsDead)
            .Select(t => t.npc.AsNpcMapInfo(t.index))
            .ToList();

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

        await _broadcastService.NotifyPlayerLeave(Players, player, warpEffect);
    }

    public bool IsTileOccupied(Coords coords)
    {
        return Players.Any(p => p.Character != null && !p.Character.Hidden &&
                                p.Character.X == coords.X && p.Character.Y == coords.Y)
            || Npcs.Any(n => n.X == coords.X && n.Y == coords.Y);
    }

    private int GetNextItemIndex(int seed = 1)
    {
        if (Items.ContainsKey(seed))
            return GetNextItemIndex(seed + 1);
        return seed;
    }

    public Task<bool> SitInChair(PlayerState player, Coords coords)
    {
        return _mapController.SitInChairAsync(player, coords, this);
    }

    public Task<bool> StandFromChair(PlayerState player)
    {
        return _mapController.StandFromChairAsync(player, this);
    }

    public async Task Tick()
    {
        if (Players.Any() is false)
            return;

        // Process item protection timers
        _mapController.ProcessItemProtection(this);

        // Handle NPC respawns
        await _mapController.ProcessNpcRespawnsAsync(this);

        // Process NPC actions (movement and combat), returns attacked player IDs
        var attackedPlayerIds = await _mapController.ProcessNpcActionsAsync(this);

        // Track recovery timer and recover players periodically
        _playerRecoverTicks++;
        if (_playerRecoverTicks >= _playerRecoverRate)
        {
            _playerRecoverTicks = 0;
            // Exclude players who were attacked this tick from recovery
            await _mapController.ProcessPlayerRecoveryAsync(this, attackedPlayerIds);
        }
    }
}