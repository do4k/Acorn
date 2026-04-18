using Acorn.Extensions;
using Acorn.Game.Services;
using Acorn.Net;
using Acorn.Shared.Caching;
using Acorn.World.Map;
using Acorn.World.Services.Npc;
using Acorn.World.Services.Player;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.World.Services.Map;

public class MapController : IMapController
{
    private const int NPC_ACT_RATE = 2;
    private const int NPC_BORED_THRESHOLD = 60;
    private const float SPIKE_DAMAGE_PERCENT = 0.1f;
    private const int DOOR_CLOSE_RATE = 10;
    private const int ITEM_CLEANUP_TICKS = 300; // ~5 minutes at 1 tick/sec
    private readonly IFormulaService _formulaService;
    private readonly ILogger<MapController> _logger;
    private readonly INpcCombatService _npcCombatService;
    private readonly INpcController _npcController;
    private readonly IPlayerController _playerController;
    private readonly IMapTileService _tileService;
    private readonly ICharacterCacheService _characterCache;
    private readonly IPaperdollService _paperdollService;
    private readonly WorldState _worldState;

    // Per-map quake state is tracked on MapController since it's shared
    private readonly Dictionary<int, int> _quakeTicks = new();
    private readonly Dictionary<int, int> _quakeRate = new();
    private readonly Dictionary<int, int> _quakeStrength = new();
    private readonly Random _random = new();

    public MapController(
        IMapTileService tileService,
        IMapBroadcastService broadcastService,
        INpcCombatService npcCombatService,
        INpcController npcController,
        IPlayerController playerController,
        IFormulaService formulaService,
        ILogger<MapController> logger,
        ICharacterCacheService characterCache,
        IPaperdollService paperdollService,
        WorldState worldState)
    {
        _tileService = tileService;
        _npcCombatService = npcCombatService;
        _npcController = npcController;
        _playerController = playerController;
        _formulaService = formulaService;
        _logger = logger;
        _characterCache = characterCache;
        _paperdollService = paperdollService;
        _worldState = worldState;
    }

    public async Task<bool> SitInChairAsync(PlayerState player, Coords coords, MapState map)
    {
        if (player.Character == null)
        {
            return false;
        }

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
        if (map.IsTileOccupied(coords))
        {
            _logger.LogWarning("Player {Character} tried to sit in occupied chair", player.Character.Name);
            return false;
        }

        // Check if tile is a chair
        var tile = _tileService.GetTile(map.Data, coords);
        if (tile == null)
        {
            return false;
        }

        var sitDirection = tile switch
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

        // Cache character state after position and sit state change
        await player.CacheCharacterStateAsync(_characterCache, _paperdollService);

        // Broadcast sit action
        await map.BroadcastPacket(new SitPlayerServerPacket
        {
            PlayerId = player.SessionId,
            Coords = coords,
            Direction = sitDirection.Value
        });

        _logger.LogInformation("Player {Character} sat in chair at ({X}, {Y})",
            player.Character.Name, coords.X, coords.Y);

        return true;
    }

    public async Task<bool> StandFromChairAsync(PlayerState player, MapState map)
    {
        if (player.Character == null)
        {
            return false;
        }

        if (player.Character.SitState != SitState.Chair)
        {
            return false;
        }

        player.Character.SitState = SitState.Stand;

        await map.BroadcastPacket(new SitPlayerServerPacket
        {
            PlayerId = player.SessionId,
            Coords = player.Character.AsCoords(),
            Direction = player.Character.Direction
        });

        _logger.LogInformation("Player {Character} stood from chair", player.Character.Name);

        return true;
    }

    public async Task ProcessNpcRespawnsAsync(MapState map)
    {
        var deadNpcs = map.Npcs.Values
            .Where(npc => npc.IsDead && npc.DeathTime.HasValue && !npc.IsAdminSpawned)
            .ToList();

        foreach (var npc in deadNpcs)
        {
            if (npc.DeathTime.HasValue)
            {
                var timeSinceDeath = DateTime.UtcNow - npc.DeathTime.Value;
                if (timeSinceDeath.TotalSeconds >= npc.RespawnTimeSeconds)
                {
                    // Respawn the NPC - reset all state
                    npc.IsDead = false;
                    npc.DeathTime = null;
                    npc.Hp = npc.Data.Hp;
                    npc.Opponents.Clear();
                    npc.ActTicks = 0;

                    // Calculate spawn position with variance for non-fixed NPCs
                    if (_npcController.ShouldUseSpawnVariance(npc))
                    {
                        var (spawnX, spawnY) = _npcController.FindSpawnPosition(npc, npc.SpawnX, npc.SpawnY,
                            map.Players.Values, map.Npcs.Values, map.Data);
                        npc.X = spawnX;
                        npc.Y = spawnY;
                    }
                    else
                    {
                        npc.X = npc.SpawnX;
                        npc.Y = npc.SpawnY;
                    }

                    // Reset direction using controller
                    npc.Direction = _npcController.GetSpawnDirection(npc);

                    _logger.LogInformation("NPC {NpcName} (ID: {NpcId}) respawned at ({X}, {Y})",
                        npc.Data.Name, npc.Id, npc.X, npc.Y);

                    // Broadcast respawn to all players on map
                    var npcIndex = map.Npcs.Values.ToList().IndexOf(npc);
                    await map.BroadcastPacket(new NpcAgreeServerPacket
                    {
                        Npcs = new List<NpcMapInfo>
                        {
                            npc.AsNpcMapInfo(npcIndex)
                        }
                    });
                }
            }
        }
    }

    public async Task<HashSet<int>> ProcessNpcActionsAsync(MapState map)
    {
        var aliveNpcs = map.Npcs.Values.Where(n => !n.IsDead).ToList();
        var npcList = map.Npcs.Values.ToList();

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
            {
                continue;
            }

            // Try to attack first
            var attackResult = _npcCombatService.TryAttack(npc, npcList.IndexOf(npc), map.Players.Values, _formulaService);
            if (attackResult != null)
            {
                attackUpdates.Add(attackResult);
                npc.ActTicks = 0;
            }
            else
            {
                // If not attacking, try to move (chase or wander)
                var moveResult = _npcController.TryMove(npc, map.Players.Values, map.Npcs.Values, map.Data);
                if (moveResult.Moved)
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

        // Only send position updates for NPCs within range of at least one player
        // This prevents the client from receiving updates for NPCs it has removed from memory
        if (positionUpdates.Count > 0 || attackUpdates.Count > 0)
        {
            var playersWithCharacters = map.Players.Values.Where(p => p.Character != null).ToList();

            // Filter position updates to only include NPCs within client range of at least one player
            var filteredPositionUpdates = positionUpdates
                .Where(update => playersWithCharacters.Any(p =>
                    _tileService.InClientRange(p.Character!.AsCoords(), update.Coords)))
                .ToList();

            // Attacks should always be sent since they involve players who are by definition in range
            if (filteredPositionUpdates.Count > 0 || attackUpdates.Count > 0)
            {
                await map.BroadcastPacket(new NpcPlayerServerPacket
                {
                    Positions = filteredPositionUpdates,
                    Attacks = attackUpdates
                });
            }
        }

        // Handle player deaths from NPC attacks
        var tasks = new List<Task>();
        var attackedPlayerIds = new HashSet<int>();

        foreach (var attack in attackUpdates)
        {
            attackedPlayerIds.Add(attack.PlayerId);

            if (attack.Killed == PlayerKilledState.Killed)
            {
                var deadPlayer = map.Players.Values.FirstOrDefault(p => p.SessionId == attack.PlayerId);
                if (deadPlayer?.Character != null)
                {
                    tasks.Add(_playerController.DieAsync(deadPlayer));
                }
            }
        }

        await Task.WhenAll(tasks);

        return attackedPlayerIds;
    }

    public async Task ProcessPlayerRecoveryAsync(MapState map, HashSet<int> excludePlayerIds)
    {
        var tasks = map.Players.Values
            .Where(p => !excludePlayerIds.Contains(p.SessionId))
            .Select(player => RecoverPlayerAsync(player))
            .ToList();

        await Task.WhenAll(tasks);
    }

    public void ProcessItemProtection(MapState map)
    {
        foreach (var item in map.Items.Values.Where(i => i.ProtectedTicks > 0))
        {
            item.ProtectedTicks--;
        }
    }

    private Task RecoverPlayerAsync(PlayerState player)
    {
        if (player.Character is null)
        {
            _logger.LogWarning("Player {PlayerId} has no character associated with them, skipping recovery.",
                player.SessionId);
            return Task.CompletedTask;
        }

        // Divisor is 5 for standing, 10 for sitting
        var divisor = player.Character.SitState == SitState.Stand ? 5 : 10;
        var (hp, tp) = player.Character.Recover(divisor);

        return player.Send(new RecoverPlayerServerPacket
        {
            Hp = hp,
            Tp = tp
        });
    }

    public async Task WarpPlayerAsync(PlayerState player, int mapId, int x, int y, WarpEffect warpEffect)
    {
        var targetMap = _worldState.MapForId(mapId);
        if (targetMap == null)
        {
            _logger.LogWarning("Cannot warp player to map {MapId} - map not found", mapId);
            return;
        }

        await _playerController.WarpAsync(player, targetMap, x, y, warpEffect);
    }

    public async Task ProcessSpikeDamageAsync(MapState map)
    {
        var playersOnSpikes = new List<PlayerState>();

        foreach (var player in map.Players.Values)
        {
            if (player.Character == null || player.Character.Hidden || player.Character.Hp <= 0)
            {
                continue;
            }

            var tile = _tileService.GetTile(map.Data, player.Character.AsCoords());
            if (tile == MapTileSpec.TimedSpikes)
            {
                playersOnSpikes.Add(player);
            }
        }

        if (playersOnSpikes.Count == 0)
        {
            return;
        }

        // Send spike effect notification to players NOT on spikes
        var reportPacket = new EffectReportServerPacket();
        foreach (var player in map.Players.Values)
        {
            if (player.Character != null && !playersOnSpikes.Contains(player))
            {
                await player.Send(reportPacket);
            }
        }

        // Apply spike damage to players standing on spike tiles
        foreach (var player in playersOnSpikes)
        {
            if (player.Character == null)
            {
                continue;
            }

            var damage = (int)Math.Floor(player.Character.MaxHp * SPIKE_DAMAGE_PERCENT);
            damage = Math.Min(damage, player.Character.Hp);
            player.Character.Hp -= damage;

            var hpPercentage = player.Character.MaxHp > 0
                ? (int)((double)player.Character.Hp / player.Character.MaxHp * 100)
                : 0;

            // Send damage notification to nearby players
            await map.BroadcastPacket(new EffectAdminServerPacket
            {
                PlayerId = player.SessionId,
                HpPercentage = hpPercentage,
                Died = player.Character.Hp == 0,
                Damage = damage
            });

            // Send HP update to the damaged player
            await player.Send(new EffectSpecServerPacket
            {
                MapDamageType = MapDamageType.Spikes,
                MapDamageTypeData = new EffectSpecServerPacket.MapDamageTypeDataSpikes
                {
                    HpDamage = damage,
                    Hp = player.Character.Hp,
                    MaxHp = player.Character.MaxHp
                }
            });

            if (player.Character.Hp == 0)
            {
                await _playerController.DieAsync(player);
            }
        }
    }

    public async Task ProcessDoorAutoCloseAsync(MapState map)
    {
        var doorsToClose = new List<Coords>();

        foreach (var kvp in map.OpenedDoors)
        {
            kvp.Value.OpenTicks++;
            if (kvp.Value.OpenTicks >= DOOR_CLOSE_RATE)
            {
                doorsToClose.Add(kvp.Key);
            }
        }

        foreach (var coords in doorsToClose)
        {
            map.OpenedDoors.TryRemove(coords, out _);

            if (map.Players.IsEmpty)
            {
                continue;
            }

            await map.BroadcastPacket(new DoorCloseServerPacket());
        }
    }

    public void ProcessGroundItemCleanup(MapState map)
    {
        var currentTick = map.TotalTicks;
        var itemsToRemove = new List<int>();

        foreach (var kvp in map.Items)
        {
            var age = currentTick - kvp.Value.DroppedAtTick;
            if (age >= ITEM_CLEANUP_TICKS)
            {
                itemsToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in itemsToRemove)
        {
            map.Items.TryRemove(key, out _);
            _logger.LogDebug("Cleaned up expired ground item (uid: {Uid}) on map {MapId}", key, map.Id);
        }
    }

    public void ProcessNpcRecovery(MapState map)
    {
        foreach (var npc in map.Npcs.Values)
        {
            if (npc.IsDead || npc.Hp >= npc.Data.Hp)
            {
                continue;
            }

            // Only recover NPCs not currently in combat (no opponents)
            if (npc.Opponents.Count > 0)
            {
                continue;
            }

            // Recover 10% of max HP + 1 per tick (matching reoserv formula)
            var recovery = npc.Data.Hp / 10 + 1;
            npc.Hp = Math.Min(npc.Hp + recovery, npc.Data.Hp);
        }
    }

    public async Task ProcessQuakeAsync(MapState map)
    {
        var timedEffect = map.Data.TimedEffect;

        if (timedEffect != MapTimedEffect.Quake1 &&
            timedEffect != MapTimedEffect.Quake2 &&
            timedEffect != MapTimedEffect.Quake3 &&
            timedEffect != MapTimedEffect.Quake4)
        {
            return;
        }

        // Get quake config based on level
        var (minTicks, maxTicks, minStrength, maxStrength) = timedEffect switch
        {
            MapTimedEffect.Quake1 => (30, 60, 1, 3),
            MapTimedEffect.Quake2 => (20, 50, 2, 5),
            MapTimedEffect.Quake3 => (15, 40, 3, 7),
            MapTimedEffect.Quake4 => (10, 30, 5, 10),
            _ => (30, 60, 1, 3)
        };

        // Initialize rate if needed
        if (!_quakeRate.ContainsKey(map.Id))
        {
            _quakeRate[map.Id] = _random.Next(minTicks, maxTicks + 1);
            _quakeStrength[map.Id] = _random.Next(minStrength, maxStrength + 1);
            _quakeTicks[map.Id] = 0;
        }

        _quakeTicks[map.Id] = _quakeTicks.GetValueOrDefault(map.Id) + 1;

        if (_quakeTicks[map.Id] >= _quakeRate[map.Id])
        {
            var strength = _quakeStrength[map.Id];

            await map.BroadcastPacket(new EffectUseServerPacket
            {
                Effect = MapEffect.Quake,
                EffectData = new EffectUseServerPacket.EffectDataQuake
                {
                    QuakeStrength = strength
                }
            });

            // Reset for next quake
            _quakeRate[map.Id] = _random.Next(minTicks, maxTicks + 1);
            _quakeStrength[map.Id] = _random.Next(minStrength, maxStrength + 1);
            _quakeTicks[map.Id] = 0;
        }
    }
}