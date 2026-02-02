using System.Collections.Concurrent;
using Acorn.Extensions;
using Acorn.Options;
using Acorn.World.Bot;
using Acorn.World.Map;
using Acorn.World.Services.Arena;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.World.Services.Bot;

/// <summary>
///     Default implementation of arena bot service.
/// </summary>
public class ArenaBotService : IArenaBotService
{
    private readonly ILogger<ArenaBotService> _logger;
    private readonly ArenaOptions _arenaOptions;
    private readonly IArenaBotController _botController;
    private readonly IArenaService _arenaService;
    
    // EO protocol max player ID is 64008 (short integer limit)
    // Player sessions use 1-50000, bots use 50001-64008 to avoid collisions
    public const int BOT_ID_START = 50001;
    public const int BOT_ID_MAX = 64008;
    private static int _nextBotId = BOT_ID_START;

    private static readonly string[] BotNames =
    [
        "Warrior", "Mage", "Archer", "Rogue", "Paladin",
        "Knight", "Assassin", "Berserker", "Cleric", "Ranger"
    ];

    public ArenaBotService(
        IOptions<ArenaOptions> arenaOptions,
        IArenaBotController botController,
        IArenaService arenaService,
        ILogger<ArenaBotService> logger)
    {
        _arenaOptions = arenaOptions.Value;
        _botController = botController;
        _arenaService = arenaService;
        _logger = logger;
    }

    public async Task SpawnBotsForQueueAsync(MapState map)
    {
        var arenaConfig = _arenaOptions.Arenas.FirstOrDefault(a => a.Map == map.Id);
        if (arenaConfig?.BotSettings is null || !arenaConfig.BotSettings.Enabled)
        {
            _logger.LogTrace("[BOT DEBUG] Map {MapId}: Bot settings null or disabled", map.Id);
            return;
        }

        // Count existing bots
        var allBots = map.ArenaBots.ToList();
        _logger.LogTrace("[BOT DEBUG] Map {MapId}: Total bots in ArenaBots collection: {Count}", 
            map.Id, allBots.Count);
        
        foreach (var bot in allBots)
        {
            _logger.LogTrace("[BOT DEBUG] - Bot {Name} (ID: {Id}) at ({X},{Y}), IsInArena={IsInArena}",
                bot.Name, bot.Id, bot.X, bot.Y, bot.IsInArena);
        }

        // Count real players in queue
        var realPlayersInQueue = CountRealPlayersInQueue(map, arenaConfig);
        var botsInQueue = map.ArenaBots.Count(b => !b.IsInArena);
        var botsInArena = map.ArenaPlayers.Count(ap => ap.PlayerId >= BOT_ID_START);

        var totalInQueue = realPlayersInQueue + botsInQueue;
        var totalInArena = map.ArenaPlayers.Count;

        _logger.LogDebug("[BOT DEBUG] Map {MapId}: RealPlayers={RealPlayers}, BotsInQueue={BotsInQueue}, BotsInArena={BotsInArena}, TotalInQueue={TotalInQueue}, TotalInArena={TotalInArena}",
            map.Id, realPlayersInQueue, botsInQueue, botsInArena, totalInQueue, totalInArena);

        // Calculate how many more bots we need
        // Fill queue to max capacity, but leave room if arena is running
        var targetQueueSize = totalInArena > 0 ? arenaConfig.Block : arenaConfig.BotSettings.MinBots;
        var botsNeeded = Math.Max(0, targetQueueSize - totalInQueue);

        _logger.LogDebug("[BOT DEBUG] Map {MapId}: TargetQueueSize={TargetQueueSize}, BotsNeeded={BotsNeeded}",
            map.Id, targetQueueSize, botsNeeded);

        // Spawn bots up to available spawn points
        var availableSpawns = GetAvailableQueueSpawns(map, arenaConfig);
        botsNeeded = Math.Min(botsNeeded, availableSpawns.Count);

        _logger.LogDebug("[BOT DEBUG] Map {MapId}: AvailableSpawns={AvailableSpawns}, FinalBotsNeeded={BotsNeeded}",
            map.Id, availableSpawns.Count, botsNeeded);

        if (botsNeeded == 0)
        {
            _logger.LogTrace("[BOT DEBUG] Map {MapId}: No bots needed (already have {ExistingBots})", 
                map.Id, botsInQueue);
        }

        for (var i = 0; i < botsNeeded; i++)
        {
            var spawn = availableSpawns[i];
            var bot = CreateBot(map.Id, spawn.spawnIndex, spawn.coords);
            map.ArenaBots.Add(bot);

            _logger.LogInformation("[BOT] Spawned arena bot {BotName} (ID: {BotId}) at queue position ({X},{Y}) on map {MapId}",
                bot.Name, bot.Id, bot.X, bot.Y, map.Id);

            // Broadcast bot appearance to all players on map
            await BroadcastBotSpawnAsync(map, bot);
        }
    }

    public async Task RemoveBotsFromQueueAsync(MapState map, int slotsNeeded)
    {
        var queuedBots = map.ArenaBots.Where(b => !b.IsInArena).Take(slotsNeeded).ToList();

        foreach (var bot in queuedBots)
        {
            map.ArenaBots = new ConcurrentBag<ArenaBotState>(map.ArenaBots.Except([bot]));
            await BroadcastBotRemovalAsync(map, bot);

            _logger.LogInformation("Removed arena bot {BotName} (ID: {BotId}) from queue on map {MapId}",
                bot.Name, bot.Id, map.Id);
        }
    }

    public async Task ProcessBotActionsAsync(MapState map)
    {
        var arenaConfig = _arenaOptions.Arenas.FirstOrDefault(a => a.Map == map.Id);
        if (arenaConfig is null)
        {
            return;
        }

        // Process actions for bots in arena
        var arenaBots = map.ArenaBots.Where(b => b.IsInArena).ToList();

        foreach (var bot in arenaBots)
        {
            bot.ActTicks++;

            // Bot acts every 2-4 ticks (slower than players for balance)
            var actDelay = Random.Shared.Next(2, 5);
            if (bot.ActTicks < actDelay)
            {
                continue;
            }

            bot.ActTicks = 0;

            // Find nearest enemy (bot or player)
            var nearestEnemy = _botController.FindNearestEnemy(bot, map);
            if (nearestEnemy.HasValue)
            {
                var (isBot, targetId, targetX, targetY) = nearestEnemy.Value;
                var distance = Math.Abs(bot.X - targetX) + Math.Abs(bot.Y - targetY);

                if (distance <= 1)
                {
                    // Attack if adjacent
                    var matchEnded = await _botController.AttackAsync(bot, targetId, isBot, map);
                    
                    // If match ended, eject all participants
                    if (matchEnded)
                    {
                        await HandleMatchEndAsync(map, arenaConfig);
                        return; // Stop processing, arena is done
                    }
                }
                else
                {
                    // Move toward enemy
                    await _botController.MoveTowardAsync(bot, targetX, targetY, map);
                }
            }
            else
            {
                // No enemies, wander randomly within arena bounds
                await _botController.WanderAsync(bot, map, arenaConfig);
            }
        }

        // Process idle actions for bots in queue (occasional face direction changes)
        var queueBots = map.ArenaBots.Where(b => !b.IsInArena).ToList();
        foreach (var bot in queueBots)
        {
            bot.ActTicks++;

            // Occasionally change facing direction (every 10-20 ticks)
            if (bot.ActTicks >= Random.Shared.Next(10, 21))
            {
                bot.ActTicks = 0;
                await _botController.IdleAnimationAsync(bot, map);
            }
        }
    }

    public async Task HandleBotDeathAsync(ArenaBotState bot, MapState map)
    {
        // Remove bot from map
        map.ArenaBots = new ConcurrentBag<ArenaBotState>(map.ArenaBots.Except([bot]));

        // Broadcast bot removal
        await BroadcastBotRemovalAsync(map, bot);

        _logger.LogInformation("Arena bot {BotName} (ID: {BotId}) was killed on map {MapId}",
            bot.Name, bot.Id, map.Id);
    }

    public async Task ClearBotsAsync(MapState map)
    {
        var bots = map.ArenaBots.ToList();
        map.ArenaBots.Clear();

        foreach (var bot in bots)
        {
            await BroadcastBotRemovalAsync(map, bot);
        }

        _logger.LogInformation("Cleared all arena bots from map {MapId}", map.Id);
    }

    #region Private Helpers

    private ArenaBotState CreateBot(int mapId, int spawnIndex, Coords coords)
    {
        var botId = Interlocked.Increment(ref _nextBotId);
        
        // Ensure we don't exceed EO protocol limit
        if (botId > BOT_ID_MAX)
        {
            _logger.LogError("Bot ID {BotId} exceeds EO protocol limit of {MaxId}! Cannot create more bots.", botId, BOT_ID_MAX);
            throw new InvalidOperationException($"Cannot create more bots - ID limit of {BOT_ID_MAX} reached");
        }
        
        var baseName = BotNames[Random.Shared.Next(BotNames.Length)];
        var botName = baseName; // Remove [BOT] prefix to test if that's causing issues

        return new ArenaBotState
        {
            Id = botId,
            Name = botName,
            X = coords.X,
            Y = coords.Y,
            Direction = Direction.Down,
            MapId = mapId,
            SpawnIndex = spawnIndex,
            IsInArena = false,
            ActTicks = 0,
            TargetBotId = 0
        };
    }

    private int CountRealPlayersInQueue(MapState map, Acorn.Options.Arena arenaConfig)
    {
        _logger.LogDebug("[QUEUE DEBUG] Total players on map {MapId}: {Count}", map.Id, map.Players.Count);
        _logger.LogDebug("[QUEUE DEBUG] Total ArenaPlayers: {Count}", map.ArenaPlayers.Count);
        
        // Log each player's position
        foreach (var p in map.Players)
        {
            if (p.Character != null)
            {
                var isInArena = map.ArenaPlayers.Any(ap => ap.PlayerId == p.SessionId);
                _logger.LogDebug("[QUEUE DEBUG] Player {Name} (ID: {Id}) at ({X},{Y}), InArena: {InArena}", 
                    p.Character.Name, 
                    p.SessionId, 
                    p.Character.X, 
                    p.Character.Y,
                    isInArena);
            }
            else
            {
                _logger.LogDebug("[QUEUE DEBUG] Player ID {Id} has NULL character", p.SessionId);
            }
        }
        
        // Log spawn points being checked
        _logger.LogDebug("[QUEUE DEBUG] Checking {Count} spawn points:", arenaConfig.Spawns.Count);
        foreach (var spawn in arenaConfig.Spawns)
        {
            _logger.LogDebug("[QUEUE DEBUG] Spawn position: ({X},{Y})", spawn.From.X, spawn.From.Y);
        }
        
        var count = arenaConfig.Spawns.Count(spawn =>
            map.Players.Any(p =>
                p.Character != null &&
                p.Character.X == spawn.From.X &&
                p.Character.Y == spawn.From.Y &&
                !map.ArenaPlayers.Any(ap => ap.PlayerId == p.SessionId)));
        
        _logger.LogDebug("[QUEUE DEBUG] Real players in queue: {Count}", count);
        return count;
    }

    private List<(int spawnIndex, Coords coords)> GetAvailableQueueSpawns(MapState map, Acorn.Options.Arena arenaConfig)
    {
        var available = new List<(int spawnIndex, Coords coords)>();

        for (var i = 0; i < arenaConfig.Spawns.Count; i++)
        {
            var spawn = arenaConfig.Spawns[i];
            var coords = spawn.From;

            // Check if spawn is occupied by player
            var hasPlayer = map.Players.Any(p =>
                p.Character != null &&
                p.Character.X == coords.X &&
                p.Character.Y == coords.Y);

            // Check if spawn is occupied by bot
            var hasBot = map.ArenaBots.Any(b =>
                b.X == coords.X &&
                b.Y == coords.Y);

            if (!hasPlayer && !hasBot)
            {
                available.Add((i, coords));
            }
        }

        return available;
    }

    private async Task BroadcastBotSpawnAsync(MapState map, ArenaBotState bot)
    {
        _logger.LogDebug("[BOT] Broadcasting spawn for bot {BotName} (ID: {BotId}) to {PlayerCount} players",
            bot.Name, bot.Id, map.Players.Count);

        // Send as a character appearing on the map
        await map.BroadcastPacket(new PlayersAgreeServerPacket
        {
            Nearby = new NearbyInfo
            {
                Characters = new List<CharacterMapInfo> { bot.AsCharacterMapInfo(WarpEffect.None) },
                Items = new List<ItemMapInfo>(),
                Npcs = new List<NpcMapInfo>()
            }
        });

        _logger.LogInformation("[BOT] Bot {BotName} (ID: {BotId}) is now visible to players at ({X},{Y})",
            bot.Name, bot.Id, bot.X, bot.Y);
    }

    private async Task BroadcastBotRemovalAsync(MapState map, ArenaBotState bot)
    {
        _logger.LogDebug("[BOT] Broadcasting removal for bot {BotName} (ID: {BotId})",
            bot.Name, bot.Id);

        // Send player leave packets
        await map.BroadcastPacket(new PlayersRemoveServerPacket
        {
            PlayerId = bot.Id
        });

        await map.BroadcastPacket(new AvatarRemoveServerPacket
        {
            PlayerId = bot.Id,
            WarpEffect = WarpEffect.None
        });

        _logger.LogInformation("[BOT] Bot {BotName} (ID: {BotId}) removed from map",
            bot.Name, bot.Id);
    }

    private async Task HandleMatchEndAsync(MapState map, Acorn.Options.Arena arenaConfig)
    {
        _logger.LogInformation("[ARENA] Handling match end on map {MapId}", map.Id);

        // Eject all remaining participants (should be just the winner)
        var remainingPlayers = map.Players.Where(p => map.ArenaPlayers.Any(ap => ap.PlayerId == p.SessionId)).ToList();
        
        foreach (var player in remainingPlayers)
        {
            _logger.LogInformation("[ARENA] Ejecting winner {PlayerName} from arena", player.Character?.Name ?? "Unknown");
            await _arenaService.HandleArenaDeathAsync(player);
        }

        // Remove all remaining bots from arena
        var remainingBots = map.ArenaBots.Where(b => b.IsInArena).ToList();
        foreach (var bot in remainingBots)
        {
            bot.IsInArena = false;
            await BroadcastBotRemovalAsync(map, bot);
            _logger.LogInformation("[ARENA] Removed bot {BotName} (ID: {BotId}) from arena", bot.Name, bot.Id);
        }

        // Clear arena players list
        map.ArenaPlayers.Clear();
        
        _logger.LogInformation("[ARENA] Match cleanup complete for map {MapId}", map.Id);
    }

    #endregion
}
