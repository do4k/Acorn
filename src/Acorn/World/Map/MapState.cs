using System.Collections.Concurrent;
using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Net;
using Acorn.World.Npc;
using Acorn.World.Services;
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
    private readonly IFormulaService _formulaService;
    private readonly IMapTileService _tileService;
    private readonly IMapBroadcastService _broadcastService;
    private readonly INpcCombatService _npcCombatService;

    // Settings - should come from configuration
    private const int DROP_DISTANCE = 2;
    private const int DROP_PROTECT_TICKS = 300; // ~3 seconds at 10 ticks/sec
    private const int NPC_ACT_RATE = 2; // Ticks between NPC actions
    private const int NPC_BORED_THRESHOLD = 60; // Ticks before NPC forgets an opponent

    public MapState(
        MapWithId data,
        IDataFileRepository dataRepository,
        IFormulaService formulaService,
        IMapTileService tileService,
        IMapBroadcastService broadcastService,
        INpcCombatService npcCombatService,
        ILogger<MapState> logger)
    {
        Id = data.Id;
        Data = data.Map;
        _logger = logger;
        _dataRepository = dataRepository;
        _formulaService = formulaService;
        _tileService = tileService;
        _broadcastService = broadcastService;
        _npcCombatService = npcCombatService;

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
                SpawnTime = npc.SpawnTime
            };

            // For SpawnType 7 (fixed NPCs), override behavior to stationary
            if (npc.SpawnType == 7)
            {
                npcState.BehaviorType = NpcBehaviorType.Stationary;
            }

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
                .Select(x => x.Character?.AsCharacterMapInfo(x.SessionId, warpEffect))
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

    public async Task<bool> SitInChair(PlayerState player, Coords coords)
    {
        if (player.Character == null) return false;

        // Check if player is standing
        if (player.Character.SitState != SitState.Stand)
        {
            _logger.LogWarning("Player {Character} tried to sit but already sitting", player.Character.Name);
            return false;
        }

        // Check distance
        var playerCoords = player.Character.AsCoords();
        if (_tileService.GetDistance(playerCoords, coords) > 1)
        {
            _logger.LogWarning("Player {Character} tried to sit in chair too far away", player.Character.Name);
            return false;
        }

        // Check if tile is occupied
        if (IsTileOccupied(coords))
        {
            _logger.LogWarning("Player {Character} tried to sit in occupied chair", player.Character.Name);
            return false;
        }

        // Check if tile is a chair
        var tile = _tileService.GetTile(Data, coords);
        if (tile == null)
            return false;

        Direction? sitDirection = tile switch
        {
            MapTileSpec.ChairDown when playerCoords.Y == coords.Y + 1 && playerCoords.X == coords.X => Direction.Down,
            MapTileSpec.ChairUp when playerCoords.Y == coords.Y - 1 && playerCoords.X == coords.X => Direction.Up,
            MapTileSpec.ChairLeft when playerCoords.X == coords.X + 1 && playerCoords.Y == coords.Y => Direction.Left,
            MapTileSpec.ChairRight when playerCoords.X == coords.X - 1 && playerCoords.Y == coords.Y => Direction.Right,
            MapTileSpec.ChairAll => playerCoords.Y == coords.Y + 1 ? Direction.Down
                                  : playerCoords.Y == coords.Y - 1 ? Direction.Up
                                  : playerCoords.X == coords.X + 1 ? Direction.Left
                                  : playerCoords.X == coords.X - 1 ? Direction.Right
                                  : (Direction?)null,
            _ => null
        };

        if (sitDirection == null)
        {
            _logger.LogWarning("Player {Character} tried to sit from wrong direction", player.Character.Name);
            return false;
        }

        // Update player state
        player.Character.X = coords.X;
        player.Character.Y = coords.Y;
        player.Character.Direction = sitDirection.Value;
        player.Character.SitState = SitState.Chair;

        // Broadcast sit action
        await BroadcastPacket(new SitPlayerServerPacket
        {
            PlayerId = player.SessionId,
            Coords = coords,
            Direction = sitDirection.Value
        });

        _logger.LogInformation("Player {Character} sat in chair at ({X}, {Y})",
            player.Character.Name, coords.X, coords.Y);

        return true;
    }

    public async Task<bool> StandFromChair(PlayerState player)
    {
        if (player.Character == null) return false;

        if (player.Character.SitState != SitState.Chair)
            return false;

        player.Character.SitState = SitState.Stand;

        await BroadcastPacket(new SitPlayerServerPacket
        {
            PlayerId = player.SessionId,
            Coords = player.Character.AsCoords(),
            Direction = player.Character.Direction
        });

        _logger.LogInformation("Player {Character} stood from chair", player.Character.Name);

        return true;
    }

    public async Task Tick()
    {
        if (Players.Any() is false)
            return;

        // Decrease item protection timers
        foreach (var item in Items.Values.Where(i => i.ProtectedTicks > 0))
        {
            item.ProtectedTicks--;
        }

        // Handle NPC respawns
        var deadNpcs = Npcs.Where(npc => npc.IsDead && npc.DeathTime.HasValue).ToList();
        foreach (var npc in deadNpcs)
        {
            if (npc.DeathTime.HasValue)
            {
                var timeSinceDeath = DateTime.UtcNow - npc.DeathTime.Value;
                if (timeSinceDeath.TotalSeconds >= npc.RespawnTimeSeconds)
                {
                    // Respawn the NPC
                    npc.IsDead = false;
                    npc.DeathTime = null;
                    npc.Hp = npc.Data.Hp;
                    npc.X = npc.SpawnX;
                    npc.Y = npc.SpawnY;

                    _logger.LogInformation("NPC {NpcName} (ID: {NpcId}) respawned at ({X}, {Y})",
                        npc.Data.Name, npc.Id, npc.X, npc.Y);

                    // Broadcast respawn to all players on map
                    var npcIndex = Npcs.ToList().IndexOf(npc);
                    await BroadcastPacket(new NpcAgreeServerPacket
                    {
                        Npcs = new List<NpcMapInfo>
                        {
                            npc.AsNpcMapInfo(npcIndex)
                        }
                    });
                }
            }
        }

        List<Task> tasks = new();

        // Only move/act NPCs that are alive and can attack (aggressive/passive)
        var aliveNpcs = Npcs.Where(n => !n.IsDead).ToList();
        var npcList = Npcs.ToList();

        var positionUpdates = new List<NpcUpdatePosition>();
        var attackUpdates = new List<NpcUpdateAttack>();

        foreach (var npc in aliveNpcs)
        {
            // Increment act ticks and process opponent boredom
            npc.ActTicks += NPC_ACT_RATE;
            _npcCombatService.ProcessOpponentBoredom(npc, NPC_ACT_RATE, NPC_BORED_THRESHOLD);

            // Only act if enough time has passed based on spawn type speed
            var actRate = _npcCombatService.GetActRate(npc.SpawnType);
            if (actRate == 0 || npc.ActTicks < actRate)
                continue;

            // Try to attack first
            var attackResult = _npcCombatService.TryAttack(npc, npcList.IndexOf(npc), Players, _formulaService);
            if (attackResult != null)
            {
                attackUpdates.Add(attackResult);
                npc.ActTicks = 0;
            }
            else
            {
                // If not attacking, try to move (chase or wander)
                var moveResult = MoveNpc(npc);
                if (moveResult.Item2)
                {
                    positionUpdates.Add(new NpcUpdatePosition
                    {
                        NpcIndex = npcList.IndexOf(npc),
                        Coords = new Coords { X = npc.X, Y = npc.Y },
                        Direction = npc.Direction
                    });
                    npc.ActTicks = 0;
                }
            }
        }

        // Send NPC updates to all players
        if (positionUpdates.Count > 0 || attackUpdates.Count > 0)
        {
            tasks.Add(BroadcastPacket(new NpcPlayerServerPacket
            {
                Positions = positionUpdates,
                Attacks = attackUpdates
            }));
        }

        // Handle player deaths from NPC attacks
        foreach (var attack in attackUpdates.Where(a => a.Killed == PlayerKilledState.Killed))
        {
            var deadPlayer = Players.FirstOrDefault(p => p.SessionId == attack.PlayerId);
            if (deadPlayer?.Character != null)
            {
                _logger.LogInformation("Player {PlayerName} was killed by NPC", deadPlayer.Character.Name);
                // TODO: Handle player death (respawn, drop items, etc.)
            }
        }

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
        // Check if this NPC should move this tick
        if (!npc.ShouldMove())
        {
            return (npc, false);
        }

        // Get next direction based on NPC behavior
        var newDirection = npc.GetNextDirection();
        var nextCoords = npc.NextCoords(newDirection);

        // Boundary check
        if (nextCoords.X < 0 || nextCoords.Y < 0 || nextCoords.X > Data.Width || nextCoords.Y > Data.Height)
        {
            // If we hit a boundary, consider changing direction for wandering NPCs
            if (npc.BehaviorType == NpcBehaviorType.Wander)
            {
                npc.LastDirectionChange = DateTime.UtcNow.AddSeconds(-5); // Force direction change next tick
            }
            return (npc, false);
        }

        // Check for player collision
        if (Players.Any(x => x.Character?.AsCoords().Equals(nextCoords) == true))
        {
            return (npc, false);
        }

        // Check for NPC collision
        if (Npcs.Any(x => x != npc && x.AsCoords().Equals(nextCoords)))
        {
            return (npc, false);
        }

        // Check tile walkability
        var row = Data.TileSpecRows.Where(x => x.Y == nextCoords.Y).ToList();
        var tile = row.SelectMany(x => x.Tiles)
            .FirstOrDefault(x => x.X == nextCoords.X);

        if (tile is not null)
        {
            if (_tileService.IsNpcWalkable(tile.TileSpec) is false)
            {
                // Hit an obstacle, might want to change direction
                if (npc.BehaviorType == NpcBehaviorType.Wander)
                {
                    npc.LastDirectionChange = DateTime.UtcNow.AddSeconds(-3);
                }
                return (npc, false);
            }
        }

        // Successfully move
        npc.X = nextCoords.X;
        npc.Y = nextCoords.Y;
        npc.Direction = newDirection;
        return (npc, true);
    }
}