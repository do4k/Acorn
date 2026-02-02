using Acorn.Net;
using Acorn.Net.PacketHandlers.Player.Warp;
using Acorn.Options;
using Acorn.World.Bot;
using Acorn.World.Map;
using Acorn.World.Services.Bot;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.World.Services.Arena;

/// <summary>
///     Default implementation of arena operations.
/// </summary>
public class ArenaService : IArenaService
{
    private readonly ILogger<ArenaService> _logger;
    private readonly ArenaOptions _arenaOptions;
    private readonly IArenaBotService? _botService;

    public ArenaService(
        IOptions<ArenaOptions> arenaOptions,
        ILogger<ArenaService> logger,
        IArenaBotService? botService = null)
    {
        _arenaOptions = arenaOptions.Value;
        _logger = logger;
        _botService = botService;
    }

    public async Task ProcessTimedArenaAsync(MapState map)
    {
        // Find arena config for this map
        var arenaConfig = _arenaOptions.Arenas.FirstOrDefault(a => a.Map == map.Id);
        if (arenaConfig is null)
        {
            return;
        }

        // Spawn bots to fill queue if enabled
        if (_botService is not null)
        {
            await _botService.SpawnBotsForQueueAsync(map);
        }

        // Increment tick counter
        map.IncrementArenaTicks();

        // Check if it's time to launch
        if (map.ArenaTicks < arenaConfig.Rate)
        {
            return;
        }

        // Reset tick counter
        map.ResetArenaTicks();

        // Check if arena is already full
        if (map.ArenaPlayers.Count >= arenaConfig.Block)
        {
            // Send "arena full" notification (ArenaDropServerPacket has no properties)
            await map.BroadcastPacket(new ArenaDropServerPacket());
            return;
        }

        // Find players standing on queue positions (spawn.from)
        var queuedPlayers = new List<(PlayerState player, ArenaSpawn spawn)>();

        foreach (var spawn in arenaConfig.Spawns)
        {
            var player = map.Players.FirstOrDefault(p =>
                p.Character != null &&
                p.Character.X == spawn.From.X &&
                p.Character.Y == spawn.From.Y &&
                !map.ArenaPlayers.Any(ap => ap.PlayerId == p.SessionId));

            if (player != null)
            {
                queuedPlayers.Add((player, spawn));
                _logger.LogDebug("[ARENA] Found player {Name} in queue at ({X},{Y})", 
                    player.Character?.Name, spawn.From.X, spawn.From.Y);
            }
        }

        // Find bots standing on queue positions
        var queuedBots = new List<(ArenaBotState bot, ArenaSpawn spawn)>();
        foreach (var spawn in arenaConfig.Spawns)
        {
            var bot = map.ArenaBots.FirstOrDefault(b =>
                !b.IsInArena &&
                b.X == spawn.From.X &&
                b.Y == spawn.From.Y);

            if (bot != null)
            {
                queuedBots.Add((bot, spawn));
            }
        }

        // Calculate how many entities to launch (players + bots)
        var availableSlots = arenaConfig.Block - map.ArenaPlayers.Count;
        var playersToLaunch = queuedPlayers.Take(availableSlots).ToList();
        var botsToLaunch = queuedBots.Take(availableSlots - playersToLaunch.Count).ToList();

        var totalToLaunch = playersToLaunch.Count + botsToLaunch.Count;

        // Need at least 2 entities (players or bots) to launch
        if (totalToLaunch < 2 && map.ArenaPlayers.Count == 0)
        {
            return; // Not enough participants queued
        }

        if (totalToLaunch == 0)
        {
            return; // No new participants to add
        }

        // Launch arena - send notification with total count
        var totalParticipants = map.ArenaPlayers.Count + totalToLaunch;
        await map.BroadcastPacket(new ArenaUseServerPacket
        {
            PlayersCount = totalParticipants
        });

        // Warp players to battle positions and add to arena
        foreach (var (player, spawn) in playersToLaunch)
        {
            if (player.Character is null) continue;

            // Add to arena players list
            map.ArenaPlayers.Add(new ArenaPlayer
            {
                PlayerId = player.SessionId,
                Kills = 0
            });

            // Warp player to battle position (local warp)
            var warpSession = new WarpSession(spawn.To.X, spawn.To.Y, player, map, WarpEffect.None);
            player.WarpSession = warpSession;
            await warpSession.Execute();

            _logger.LogInformation("Player {PlayerName} (ID: {PlayerId}) joined arena on map {MapId}",
                player.Character.Name, player.SessionId, map.Id);
        }

        // Warp bots to battle positions and add to arena
        foreach (var (bot, spawn) in botsToLaunch)
        {
            // Add to arena players list
            map.ArenaPlayers.Add(new ArenaPlayer
            {
                PlayerId = bot.Id,
                Kills = 0
            });

            // Update bot position and state
            bot.X = spawn.To.X;
            bot.Y = spawn.To.Y;
            bot.IsInArena = true;

            _logger.LogInformation("[ARENA] Bot {BotName} (ID: {BotId}) joined arena on map {MapId} at position ({X},{Y})",
                bot.Name, bot.Id, map.Id, bot.X, bot.Y);

            // Broadcast bot entering arena - send movement packet
            await map.BroadcastPacket(new WalkPlayerServerPacket
            {
                PlayerId = bot.Id,
                Direction = bot.Direction,
                Coords = new Coords { X = bot.X, Y = bot.Y }
            });

            await map.BroadcastPacket(new TalkServerServerPacket
            {
                Message = $"{bot.Name} entered the arena!"
            });
        }
    }

    public async Task<bool> HandleArenaCombatAsync(PlayerState attacker, PlayerState target, MapState map)
    {
        if (attacker.Character is null || target.Character is null)
        {
            return false;
        }

        // Verify both players are in the arena
        var attackerArena = map.ArenaPlayers.FirstOrDefault(ap => ap.PlayerId == attacker.SessionId);
        var targetArena = map.ArenaPlayers.FirstOrDefault(ap => ap.PlayerId == target.SessionId);

        if (attackerArena is null || targetArena is null)
        {
            return false;
        }

        // Check distance (must be adjacent - distance <= 1)
        var distance = Math.Abs(attacker.Character.X - target.Character.X) +
                       Math.Abs(attacker.Character.Y - target.Character.Y);

        if (distance > 1)
        {
            return false;
        }

        // Instant kill - increment attacker kills
        attackerArena.Kills++;

        // Remove target from arena
        map.ArenaPlayers.Remove(targetArena);

        _logger.LogInformation("Arena kill: {AttackerName} killed {TargetName} (Kills: {Kills})",
            attacker.Character.Name, target.Character.Name, attackerArena.Kills);

        // Send kill notification based on reoserv structure
        await map.BroadcastPacket(new ArenaSpecServerPacket
        {
            PlayerId = attacker.SessionId,
            Direction = attacker.Character.Direction,
            KillsCount = attackerArena.Kills,
            KillerName = attacker.Character.Name,
            VictimName = target.Character.Name
        });

        // Handle target death
        await HandleArenaDeathAsync(target);

        // Check if only one player/bot remains (winner)
        if (map.ArenaPlayers.Count == 1)
        {
            var winnerArena = map.ArenaPlayers.First();
            var winnerPlayer = map.Players.FirstOrDefault(p => p.SessionId == winnerArena.PlayerId);
            var winnerBot = map.ArenaBots.FirstOrDefault(b => b.Id == winnerArena.PlayerId);

            string winnerName = "Unknown";
            if (winnerPlayer?.Character != null)
            {
                winnerName = winnerPlayer.Character.Name ?? "Unknown";
            }
            else if (winnerBot != null)
            {
                winnerName = winnerBot.Name;
            }

            // Send victory notification
            await map.BroadcastPacket(new ArenaAcceptServerPacket
            {
                WinnerName = winnerName,
                KillsCount = winnerArena.Kills,
                KillerName = attacker.Character.Name,
                VictimName = target.Character.Name
            });

            _logger.LogInformation("Arena ended: {WinnerName} won with {Kills} kills",
                winnerName, winnerArena.Kills);

            // Warp winner back to spawn (if player)
            if (winnerPlayer != null)
            {
                await HandleArenaDeathAsync(winnerPlayer);
            }

            // Clear arena (bots will be removed)
            map.ArenaPlayers.Clear();
            if (_botService is not null)
            {
                await _botService.ClearBotsAsync(map);
            }
        }

        return true;
    }

    public async Task HandleArenaDeathAsync(PlayerState player)
    {
        if (player.Character is null || player.CurrentMap is null)
        {
            return;
        }

        var map = player.CurrentMap;

        // Find arena config for respawn coordinates
        var arenaConfig = _arenaOptions.Arenas.FirstOrDefault(a => a.Map == map.Id);
        if (arenaConfig is null)
        {
            _logger.LogWarning("No arena config found for map {MapId}", map.Id);
            return;
        }

        var spawnX = arenaConfig.RespawnX;
        var spawnY = arenaConfig.RespawnY;

        _logger.LogInformation("Player {PlayerName} respawning from arena at ({X},{Y})",
            player.Character.Name, spawnX, spawnY);

        // Warp player to spawn (local warp within same map)
        var warpSession = new WarpSession(spawnX, spawnY, player, map, WarpEffect.None);
        player.WarpSession = warpSession;
        await warpSession.Execute();
    }

    public async Task HandleArenaAbandonmentAsync(PlayerState player, MapState map)
    {
        var arenaPlayer = map.ArenaPlayers.FirstOrDefault(ap => ap.PlayerId == player.SessionId);
        if (arenaPlayer is null)
        {
            return; // Player not in arena
        }

        // Check if player is NOT standing on a queue position (meaning they abandoned mid-match)
        var arenaConfig = _arenaOptions.Arenas.FirstOrDefault(a => a.Map == map.Id);
        if (arenaConfig is null)
        {
            return;
        }

        var isOnQueueTile = player.Character != null &&
                            arenaConfig.Spawns.Any(s =>
                                s.From.X == player.Character.X && s.From.Y == player.Character.Y);

        if (isOnQueueTile)
        {
            return; // Player is on queue tile, not abandoning
        }

        // Remove player from arena
        map.ArenaPlayers.Remove(arenaPlayer);

        _logger.LogInformation("Player {PlayerName} abandoned arena on map {MapId}",
            player.Character?.Name ?? "Unknown", map.Id);

        // If only one player remains, abort the arena
        if (map.ArenaPlayers.Count == 1)
        {
            var remainingPlayer = map.Players.FirstOrDefault(p =>
                map.ArenaPlayers.Any(ap => ap.PlayerId == p.SessionId));

            if (remainingPlayer is not null)
            {
                // Send abort message
                await map.BroadcastPacket(new TalkServerServerPacket
                {
                    Message = "The event was aborted, last opponent left -server"
                });

                // Warp remaining player to spawn
                await HandleArenaDeathAsync(remainingPlayer);
            }

            // Clear arena
            map.ArenaPlayers.Clear();

            _logger.LogInformation("Arena aborted on map {MapId} - last opponent left", map.Id);
        }
    }

    public bool IsPlayerInArena(PlayerState player, MapState map)
    {
        return map.ArenaPlayers.Any(ap => ap.PlayerId == player.SessionId);
    }
}
