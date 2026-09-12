using System.Collections.Concurrent;
using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Game.Services;
using Acorn.Net;
using Acorn.World.Npc;
using Acorn.World.Services.Map;
using Acorn.World.Services.Npc;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.World.Map;

/// <summary>
///     Represents an item on the map with protection timer
/// </summary>
public class MapItem
{
    public required int Id { get; set; }
    public required int Amount { get; set; }
    public required Coords Coords { get; set; }
    public int OwnerId { get; set; }
    public int ProtectedTicks { get; set; }

    /// <summary>
    ///     Tick count when this item was dropped on the ground.
    ///     Used for ground item cleanup after expiry.
    /// </summary>
    public int DroppedAtTick { get; set; }
}

/// <summary>
///     Tracks an opened door with its auto-close timer.
/// </summary>
public class OpenedDoor
{
    public required Coords Coords { get; set; }
    public int OpenTicks { get; set; }
}

public class ArenaPlayer
{
    public required int PlayerId { get; set; }
    public required int SessionId { get; set; }
    public int Kills { get; set; }
    public bool IsDead { get; set; }
}

public class MapState
{
    private readonly IMapBroadcastService _broadcastService;
    private readonly IMapController _mapController;
    private readonly IMapTileService _tileService;
    private readonly IPaperdollService _paperdollService;
    private readonly int _playerRecoverRate;
    private readonly bool _isArenaEnabled;
    private readonly int _arenaSpawnInterval;
    // Tick counters for periodic events
    private int _playerRecoverTicks;
    private int _arenaTicks;
    private int _spikeTicks;
    private int _npcRecoverTicks;
    private int _itemCleanupTicks;
    private int _totalTicks;

    // Arena state
    public ConcurrentQueue<int> ArenaQueue { get; } = new();
    public List<ArenaPlayer> ArenaPlayers { get; } = new();
    public bool IsArenaMap { get; private set; }

    public MapState(
        MapWithId data,
        IDataFileRepository dataRepository,
        IMapBroadcastService broadcastService,
        IMapController mapController,
        INpcController npcController,
        IMapTileService tileService,
        IPaperdollService paperdollService,
        int playerRecoverRate,
        bool isArenaEnabled,
        int arenaSpawnInterval,
        ILogger<MapState> logger)
    {
        Id = data.Id;
        Data = data.Map;
        _broadcastService = broadcastService;
        _mapController = mapController;
        _tileService = tileService;
        _paperdollService = paperdollService;
        _playerRecoverRate = playerRecoverRate;
        _isArenaEnabled = isArenaEnabled;
        _arenaSpawnInterval = arenaSpawnInterval;
        IsArenaMap = _isArenaEnabled && data.Id == 1;

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

            // Add spawn variance for mobile NPCs so they don't stack on top of each
            // other at map load, while staying off walls and inside NPC boundaries.
            if (npcController.ShouldUseSpawnVariance(npcState))
            {
                var (spawnX, spawnY) = npcController.FindSpawnPosition(npcState, npcState.SpawnX, npcState.SpawnY,
                    Enumerable.Empty<PlayerState>(), Npcs.Values, data.Map);
                npcState.X = spawnX;
                npcState.Y = spawnY;
            }

            var npcIndex = Npcs.Count;
            npcState.Index = npcIndex;
            Npcs.TryAdd(npcIndex, npcState);
        }
    }

    public int Id { get; set; }
    public Emf Data { get; set; }

    public ConcurrentDictionary<int, NpcState> Npcs { get; set; } = new();
    public ConcurrentDictionary<int, PlayerState> Players { get; set; } = new();
    public ConcurrentDictionary<int, MapItem> Items { get; set; } = new();
    public ConcurrentDictionary<Coords, MapChest> Chests { get; set; } = new();

    /// <summary>
    ///     Doors currently open with auto-close timers.
    /// </summary>
    public ConcurrentDictionary<Coords, OpenedDoor> OpenedDoors { get; set; } = new();

    /// <summary>
    ///     Remaining ticks for the currently playing jukebox track.
    ///     When > 0, the jukebox is busy and cannot play another track.
    /// </summary>
    public int JukeboxTicks { get; set; }

    /// <summary>
    ///     Name of the player who last played the jukebox.
    /// </summary>
    public string? JukeboxPlayerName { get; set; }

    /// <summary>
    ///     Active wedding ceremony on this map, if any.
    /// </summary>
    public Wedding? Wedding { get; set; }

    /// <summary>
    ///     Tick counter for the wedding ceremony progression.
    /// </summary>
    public int WeddingTicks { get; set; }

    /// <summary>
    ///     Current total tick count for this map, used for item drop timestamps.
    /// </summary>
    public int TotalTicks => _totalTicks;

    public bool HasPlayer(PlayerState player)
    {
        return Players.ContainsKey(player.SessionId);
    }

    public IEnumerable<PlayerState> PlayersExcept(PlayerState? except)
    {
        return Players.Values.Where(x => except is null || x.SessionId != except.SessionId);
    }

    public async Task BroadcastPacket(IPacket packet, PlayerState? except = null)
    {
        await _broadcastService.BroadcastPacket(Players.Values, packet, except);
    }

    /// <summary>
    ///     Builds a range-filtered view of nearby entities relative to <paramref name="observer" />.
    ///     The observer's own character is included, matching eoserv's refresh behaviour.
    /// </summary>
    public NearbyInfo AsNearbyInfo(PlayerState observer)
    {
        var origin = observer.Character?.AsCoords() ?? new Coords();

        return new NearbyInfo
        {
            Characters = Players.Values
                .Where(x => x.Character is not null)
                .Where(x => _tileService.InClientRange(origin, x.Character!.AsCoords()))
                .Select(x => x.Character!.AsCharacterMapInfo(x.SessionId, WarpEffect.None, _paperdollService))
                .ToList(),
            Items = Items
                .Where(kvp => _tileService.InClientRange(origin, kvp.Value.Coords))
                .Select(kvp => new ItemMapInfo
                {
                    Uid = kvp.Key,
                    Id = kvp.Value.Id,
                    Coords = kvp.Value.Coords,
                    Amount = kvp.Value.Amount
                })
                .ToList(),
            Npcs = Npcs.Values
                .Where(npc => !npc.IsDead)
                .Where(npc => _tileService.InClientRange(origin, npc.AsCoords()))
                .OrderBy(npc => npc.Index)
                .Select(npc => npc.AsNpcMapInfo())
                .Take(252) // EO Protocol limit: NpcMapInfo uses byte field, max 252 NPCs
                .ToList()
        };
    }

    /// <summary>
    ///     Builds a range-filtered reply for a Range/Request restricted to the requested
    ///     player ids and NPC indexes. Ground items are not part of a range request.
    /// </summary>
    public NearbyInfo AsNearbyInfo(PlayerState observer,
        IReadOnlyCollection<int> playerIds,
        IReadOnlyCollection<int> npcIndexes)
    {
        var origin = observer.Character?.AsCoords() ?? new Coords();

        return new NearbyInfo
        {
            Characters = Players.Values
                .Where(x => x.Character is not null && playerIds.Contains(x.SessionId))
                .Where(x => _tileService.InClientRange(origin, x.Character!.AsCoords()))
                .Select(x => x.Character!.AsCharacterMapInfo(x.SessionId, WarpEffect.None, _paperdollService))
                .ToList(),
            Npcs = Npcs.Values
                .Where(npc => !npc.IsDead && npcIndexes.Contains(npc.Index))
                .Where(npc => _tileService.InClientRange(origin, npc.AsCoords()))
                .OrderBy(npc => npc.Index)
                .Select(npc => npc.AsNpcMapInfo())
                .ToList(),
            Items = []
        };
    }

    /// <summary>
    ///     Builds a single-character <see cref="NearbyInfo" /> used when a player appears in
    ///     view. The warp effect is applied only to that player's own character info.
    /// </summary>
    public NearbyInfo AsCharacterInfo(PlayerState player, WarpEffect warpEffect = WarpEffect.None)
    {
        if (player.Character is null)
        {
            return new NearbyInfo();
        }

        return new NearbyInfo
        {
            Characters =
            [
                player.Character.AsCharacterMapInfo(player.SessionId, warpEffect, _paperdollService)
            ],
            Npcs = [],
            Items = []
        };
    }

    public List<NpcMapInfo> AsNpcMapInfo()
    {
        return Npcs.Values
            .Where(npc => !npc.IsDead)
            .OrderBy(npc => npc.Index)
            .Select(npc => npc.AsNpcMapInfo())
            .Take(252) // EO Protocol limit: NpcMapInfo uses byte field, max 252 NPCs
            .ToList();
    }

    /// <summary>
    ///     Returns the next free NPC index for this map (the lowest index not currently in use).
    /// </summary>
    public int GetNextNpcIndex()
    {
        var index = 0;
        while (Npcs.ContainsKey(index))
        {
            index++;
        }

        return index;
    }

    public async Task NotifyEnter(PlayerState player, WarpEffect warpEffect = WarpEffect.None)
    {
        if (player.Character is null)
        {
            return;
        }

        player.Character.Map = Id;

        Players.TryAdd(player.SessionId, player);
        player.CurrentMap = this;

        await NotifyAppear(player, warpEffect);
    }

    /// <summary>
    ///     Tells only the players within view range of <paramref name="player" /> that the
    ///     player has appeared. The warp effect is applied to that player's info alone.
    /// </summary>
    public async Task NotifyAppear(PlayerState player, WarpEffect warpEffect = WarpEffect.None)
    {
        if (player.Character is null)
        {
            return;
        }

        var origin = player.Character.AsCoords();
        var packet = new PlayersAgreeServerPacket
        {
            Nearby = AsCharacterInfo(player, warpEffect)
        };

        var recipients = Players.Values
            .Where(p => p.SessionId != player.SessionId && p.Character is not null)
            .Where(p => _tileService.InClientRange(origin, p.Character!.AsCoords()))
            .ToList();

        foreach (var recipient in recipients)
        {
            await recipient.Send(packet);
        }
    }

    public async Task NotifyLeave(PlayerState player, WarpEffect warpEffect = WarpEffect.None)
    {
        Players.TryRemove(player.SessionId, out _);

        if (player.Character is null)
        {
            return;
        }

        var origin = player.Character.AsCoords();
        var recipients = Players.Values
            .Where(p => p.Character is not null)
            .Where(p => _tileService.InClientRange(origin, p.Character!.AsCoords()))
            .ToList();

        await _broadcastService.NotifyPlayerLeave(recipients, player, warpEffect);
    }

    /// <summary>
    ///     Sends removal packets for players and NPCs that were within <paramref name="player" />'s
    ///     view before the move but are no longer. Used by the walk path.
    /// </summary>
    public async Task NotifyMoveViewChangesAsync(PlayerState player, Coords oldCoords)
    {
        if (player.Character is null)
        {
            return;
        }

        var newCoords = player.Character.AsCoords();

        foreach (var other in Players.Values
                     .Where(p => p.SessionId != player.SessionId && p.Character is not null)
                     .ToList())
        {
            var otherCoords = other.Character!.AsCoords();
            var wasInRange = _tileService.InClientRange(oldCoords, otherCoords);
            var isInRange = _tileService.InClientRange(newCoords, otherCoords);

            if (wasInRange && !isInRange)
            {
                // The player can no longer see the other character, and vice versa.
                await player.Send(new AvatarRemoveServerPacket { PlayerId = other.SessionId });
                await other.Send(new AvatarRemoveServerPacket { PlayerId = player.SessionId });
            }
        }

        foreach (var npc in Npcs.Values.Where(n => !n.IsDead).ToList())
        {
            var npcCoords = npc.AsCoords();
            if (_tileService.InClientRange(oldCoords, npcCoords)
                && !_tileService.InClientRange(newCoords, npcCoords))
            {
                // The EO protocol removes an NPC from view by moving it out of bounds.
                await player.Send(new NpcPlayerServerPacket
                {
                    Positions =
                    [
                        new NpcUpdatePosition
                        {
                            NpcIndex = npc.Index,
                            Coords = new Coords { X = 252, Y = 252 },
                            Direction = Direction.Down
                        }
                    ],
                    Attacks = [],
                    Chats = []
                });
            }
        }
    }

    public bool IsTileOccupied(Coords coords)
    {
        return Players.Values.Any(p => p.Character != null && !p.Character.Hidden &&
                                p.Character.X == coords.X && p.Character.Y == coords.Y)
               || Npcs.Values.Any(n => n.X == coords.X && n.Y == coords.Y);
    }

    public int GetNextItemIndex(int seed = 1)
    {
        if (Items.ContainsKey(seed))
        {
            return GetNextItemIndex(seed + 1);
        }

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
        _totalTicks++;

        if (Players.IsEmpty)
        {
            // Still process door auto-close and NPC recovery even without players
            await _mapController.ProcessDoorAutoCloseAsync(this);
            _mapController.ProcessNpcRecovery(this);
            return;
        }

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

        // Spike damage every 2 ticks
        _spikeTicks++;
        if (_spikeTicks >= 2)
        {
            _spikeTicks = 0;
            await _mapController.ProcessSpikeDamageAsync(this);
        }

        // Door auto-close check every tick
        await _mapController.ProcessDoorAutoCloseAsync(this);

        // NPC HP recovery every 5 ticks (same rate as player recovery default)
        _npcRecoverTicks++;
        if (_npcRecoverTicks >= 5)
        {
            _npcRecoverTicks = 0;
            _mapController.ProcessNpcRecovery(this);
        }

        // Ground item cleanup every 30 ticks
        _itemCleanupTicks++;
        if (_itemCleanupTicks >= 30)
        {
            _itemCleanupTicks = 0;
            _mapController.ProcessGroundItemCleanup(this);
        }

        // Jukebox timer
        if (JukeboxTicks > 0)
        {
            JukeboxTicks--;
            if (JukeboxTicks == 0)
            {
                JukeboxPlayerName = null;
            }
        }

        // Quake effects
        await _mapController.ProcessQuakeAsync(this);

        // Process arena spawns
        if (IsArenaMap && _isArenaEnabled)
        {
            await ProcessArenaTickAsync();
        }
    }

    private async Task ProcessArenaTickAsync()
    {
        _arenaTicks++;
        if (_arenaTicks < _arenaSpawnInterval)
            return;

        _arenaTicks = 0;

        if (!ArenaQueue.TryPeek(out var playerId))
            return;

        var player = Players.Values.FirstOrDefault(p => p.SessionId == playerId);
        if (player == null)
        {
            ArenaQueue.TryDequeue(out _);
            return;
        }

        ArenaQueue.TryDequeue(out _);
        var arenaPlayer = new ArenaPlayer
        {
            PlayerId = playerId,
            SessionId = player.SessionId,
            Kills = 0,
            IsDead = false
        };
        ArenaPlayers.Add(arenaPlayer);

        await _mapController.WarpPlayerAsync(player, Id, 1, 1, WarpEffect.Admin);
    }

    public bool TryJoinArenaQueue(int sessionId)
    {
        if (!IsArenaMap || !_isArenaEnabled)
        {
            return false;
        }

        // Check if already in queue or in arena
        if (ArenaQueue.Contains(sessionId) || ArenaPlayers.Any(p => p.SessionId == sessionId))
        {
            return false;
        }

        ArenaQueue.Enqueue(sessionId);
        return true;
    }

    public void RemoveFromArena(int sessionId)
    {
        ArenaPlayers.RemoveAll(p => p.SessionId == sessionId);
    }

    public void LeaveArenaQueue(int sessionId)
    {
        // Remove only the target player while preserving the rest of the queue order
        var survivors = new List<int>();
        while (ArenaQueue.TryDequeue(out var id))
        {
            if (id != sessionId)
            {
                survivors.Add(id);
            }
        }

        foreach (var id in survivors)
        {
            ArenaQueue.Enqueue(id);
        }
    }

    /// <summary>
    ///     Register a door as opened for auto-close tracking.
    /// </summary>
    public void RegisterOpenedDoor(Coords coords)
    {
        OpenedDoors.AddOrUpdate(coords,
            _ => new OpenedDoor { Coords = coords, OpenTicks = 0 },
            (_, existing) => { existing.OpenTicks = 0; return existing; });
    }
}