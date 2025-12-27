using Acorn.Net;
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
    private readonly ILogger<MapController> _logger;
    private readonly IMapTileService _tileService;
    private readonly IMapBroadcastService _broadcastService;
    private readonly INpcCombatService _npcCombatService;
    private readonly INpcController _npcController;
    private readonly IPlayerController _playerController;
    private readonly IFormulaService _formulaService;

    private const int NPC_ACT_RATE = 2;
    private const int NPC_BORED_THRESHOLD = 60;

    public MapController(
        IMapTileService tileService,
        IMapBroadcastService broadcastService,
        INpcCombatService npcCombatService,
        INpcController npcController,
        IPlayerController playerController,
        IFormulaService formulaService,
        ILogger<MapController> logger)
    {
        _tileService = tileService;
        _broadcastService = broadcastService;
        _npcCombatService = npcCombatService;
        _npcController = npcController;
        _playerController = playerController;
        _formulaService = formulaService;
        _logger = logger;
    }

    public async Task<bool> SitInChairAsync(PlayerState player, Coords coords, MapState map)
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
        if (map.IsTileOccupied(coords))
        {
            _logger.LogWarning("Player {Character} tried to sit in occupied chair", player.Character.Name);
            return false;
        }

        // Check if tile is a chair
        var tile = _tileService.GetTile(map.Data, coords);
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
        if (player.Character == null) return false;

        if (player.Character.SitState != SitState.Chair)
            return false;

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
        var deadNpcs = map.Npcs.Where(npc => npc.IsDead && npc.DeathTime.HasValue).ToList();
        
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
                        var (spawnX, spawnY) = _npcController.FindSpawnPosition(npc, npc.SpawnX, npc.SpawnY, map.Players, map.Npcs, map.Data);
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
                    var npcIndex = map.Npcs.ToList().IndexOf(npc);
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
        var aliveNpcs = map.Npcs.Where(n => !n.IsDead).ToList();
        var npcList = map.Npcs.ToList();

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
            var attackResult = _npcCombatService.TryAttack(npc, npcList.IndexOf(npc), map.Players, _formulaService);
            if (attackResult != null)
            {
                attackUpdates.Add(attackResult);
                npc.ActTicks = 0;
            }
            else
            {
                // If not attacking, try to move (chase or wander)
                var moveResult = _npcController.TryMove(npc, map.Players, map.Npcs, map.Data);
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
            var playersWithCharacters = map.Players.Where(p => p.Character != null).ToList();
            
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
                var deadPlayer = map.Players.FirstOrDefault(p => p.SessionId == attack.PlayerId);
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
        var tasks = map.Players
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
            _logger.LogWarning("Player {PlayerId} has no character associated with them, skipping recovery.", player.SessionId);
            return Task.CompletedTask;
        }

        // Divisor is 5 for standing, 10 for sitting
        var divisor = player.Character.SitState == SitState.Stand ? 5 : 10;
        var (hp, tp) = player.Character.Recover(divisor);

        return player.Send(new RecoverPlayerServerPacket
        {
            Hp = hp,
            Tp = tp,
        });
    }
}
