using Acorn.Options;
using Acorn.World.Bot;
using Acorn.World.Map;
using Acorn.World.Services.Map;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.World.Services.Bot;

/// <summary>
///     Default implementation of arena bot controller.
///     Handles basic AI for movement, targeting, and combat.
/// </summary>
public class ArenaBotController : IArenaBotController
{
    private readonly ILogger<ArenaBotController> _logger;
    private readonly IMapTileService _tileService;

    public ArenaBotController(ILogger<ArenaBotController> logger, IMapTileService tileService)
    {
        _logger = logger;
        _tileService = tileService;
    }

    public (bool isBot, int targetId, int targetX, int targetY)? FindNearestEnemy(ArenaBotState bot, MapState map)
    {
        var nearestDistance = int.MaxValue;
        (bool isBot, int targetId, int targetX, int targetY)? nearest = null;

        // Check other bots in arena
        foreach (var otherBot in map.ArenaBots.Where(b => b.IsInArena && b.Id != bot.Id))
        {
            var distance = bot.DistanceTo(otherBot);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = (true, otherBot.Id, otherBot.X, otherBot.Y);
            }
        }

        // Check players in arena
        foreach (var player in map.Players)
        {
            if (player.Character is null) continue;
            if (!map.ArenaPlayers.Any(ap => ap.PlayerId == player.SessionId)) continue;

            var distance = Math.Abs(bot.X - player.Character.X) + Math.Abs(bot.Y - player.Character.Y);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = (false, player.SessionId, player.Character.X, player.Character.Y);
            }
        }

        return nearest;
    }

    public async Task MoveTowardAsync(ArenaBotState bot, int targetX, int targetY, MapState map)
    {
        // Simple movement AI - move in the direction that reduces distance
        var dx = targetX - bot.X;
        var dy = targetY - bot.Y;

        Direction newDirection;

        // Prioritize vertical or horizontal movement based on greater distance
        if (Math.Abs(dx) > Math.Abs(dy))
        {
            newDirection = dx > 0 ? Direction.Right : Direction.Left;
        }
        else
        {
            newDirection = dy > 0 ? Direction.Down : Direction.Up;
        }

        // Try to move in the calculated direction
        var nextCoords = bot.NextCoords(newDirection);

        // Check if tile is walkable and not occupied
        if (IsTileWalkable(map, nextCoords) && !IsTileOccupied(map, nextCoords))
        {
            var oldX = bot.X;
            var oldY = bot.Y;

            bot.X = nextCoords.X;
            bot.Y = nextCoords.Y;
            bot.Direction = newDirection;

            // Broadcast bot movement to players
            await map.BroadcastPacket(new WalkPlayerServerPacket
            {
                PlayerId = bot.Id,
                Direction = newDirection,
                Coords = new Coords { X = bot.X, Y = bot.Y }
            });

            _logger.LogDebug("[BOT] Bot {BotName} (ID: {BotId}) moved from ({OldX},{OldY}) to ({NewX},{NewY})",
                bot.Name, bot.Id, oldX, oldY, bot.X, bot.Y);
        }
        else
        {
            // Try alternate direction if blocked
            var alternateDirection = GetAlternateDirection(newDirection, dx, dy);
            var alternateCoords = bot.NextCoords(alternateDirection);

            if (IsTileWalkable(map, alternateCoords) && !IsTileOccupied(map, alternateCoords))
            {
                bot.X = alternateCoords.X;
                bot.Y = alternateCoords.Y;
                bot.Direction = alternateDirection;

                await map.BroadcastPacket(new WalkPlayerServerPacket
                {
                    PlayerId = bot.Id,
                    Direction = alternateDirection,
                    Coords = new Coords { X = bot.X, Y = bot.Y }
                });

                _logger.LogDebug("[BOT] Bot {BotName} (ID: {BotId}) moved (alternate) to ({X},{Y})",
                    bot.Name, bot.Id, bot.X, bot.Y);
            }
            else
            {
                _logger.LogDebug("[BOT] Bot {BotName} (ID: {BotId}) blocked at ({X},{Y})",
                    bot.Name, bot.Id, bot.X, bot.Y);
            }
        }
    }

    public async Task WanderAsync(ArenaBotState bot, MapState map, Acorn.Options.Arena arenaConfig)
    {
        // Random wander within arena battle zone
        var directions = new[] { Direction.Up, Direction.Down, Direction.Left, Direction.Right };
        var randomDirection = directions[Random.Shared.Next(directions.Length)];

        var nextCoords = bot.NextCoords(randomDirection);

        // Check if within arena bounds (roughly the battle zone)
        var battleZone = arenaConfig.Spawns.Select(s => s.To).ToList();
        if (battleZone.Count == 0) return;

        var minX = battleZone.Min(c => c.X) - 5;
        var maxX = battleZone.Max(c => c.X) + 5;
        var minY = battleZone.Min(c => c.Y) - 5;
        var maxY = battleZone.Max(c => c.Y) + 5;

        if (nextCoords.X < minX || nextCoords.X > maxX || nextCoords.Y < minY || nextCoords.Y > maxY)
        {
            _logger.LogTrace("[BOT] Bot {BotName} (ID: {BotId}) wander blocked by arena bounds",
                bot.Name, bot.Id);
            return; // Don't wander outside arena
        }

        if (IsTileWalkable(map, nextCoords) && !IsTileOccupied(map, nextCoords))
        {
            bot.X = nextCoords.X;
            bot.Y = nextCoords.Y;
            bot.Direction = randomDirection;

            await map.BroadcastPacket(new WalkPlayerServerPacket
            {
                PlayerId = bot.Id,
                Direction = randomDirection,
                Coords = new Coords { X = bot.X, Y = bot.Y }
            });

            _logger.LogTrace("[BOT] Bot {BotName} (ID: {BotId}) wandered to ({X},{Y})",
                bot.Name, bot.Id, bot.X, bot.Y);
        }
    }

    public async Task<bool> AttackAsync(ArenaBotState bot, int targetId, bool isBot, MapState map)
    {
        if (isBot)
        {
            // Bot vs Bot combat
            var targetBot = map.ArenaBots.FirstOrDefault(b => b.Id == targetId);
            if (targetBot is null)
            {
                _logger.LogWarning("[BOT] Bot {BotId} tried to attack bot {TargetId} but target not found",
                    bot.Id, targetId);
                return false;
            }

            // Send attack animation BEFORE processing kill
            await map.BroadcastPacket(new AttackPlayerServerPacket
            {
                PlayerId = bot.Id,
                Direction = bot.Direction
            });

            _logger.LogInformation("[BOT COMBAT] Bot {AttackerName} (ID: {AttackerId}) killed bot {TargetName} (ID: {TargetId})",
                bot.Name, bot.Id, targetBot.Name, targetBot.Id);

            // Increment attacker kills
            var attackerArena = map.ArenaPlayers.FirstOrDefault(ap => ap.PlayerId == bot.Id);
            if (attackerArena != null)
            {
                attackerArena.Kills++;

                // Broadcast kill notification
                await map.BroadcastPacket(new ArenaSpecServerPacket
                {
                    PlayerId = bot.Id,
                    Direction = bot.Direction,
                    KillsCount = attackerArena.Kills,
                    KillerName = bot.Name,
                    VictimName = targetBot.Name
                });
            }

            // Remove target bot from arena
            var targetArena = map.ArenaPlayers.FirstOrDefault(ap => ap.PlayerId == targetBot.Id);
            if (targetArena != null)
            {
                map.ArenaPlayers.Remove(targetArena);
            }

            // Handle bot death
            targetBot.IsInArena = false;
            map.ArenaBots = new System.Collections.Concurrent.ConcurrentBag<ArenaBotState>(
                map.ArenaBots.Except([targetBot]));

            await map.BroadcastPacket(new TalkServerServerPacket
            {
                Message = $"{targetBot.Name} was defeated!"
            });

            _logger.LogInformation("[BOT] Removed bot {BotName} (ID: {BotId}) after defeat",
                targetBot.Name, targetBot.Id);
            
            // Check if only one entity remains (winner)
            if (map.ArenaPlayers.Count == 1)
            {
                var winnerArena = map.ArenaPlayers.First();
                var winnerPlayer = map.Players.FirstOrDefault(p => p.SessionId == winnerArena.PlayerId);
                var winnerBot = map.ArenaBots.FirstOrDefault(b => b.Id == winnerArena.PlayerId && b.IsInArena);

                string winnerName = winnerPlayer?.Character?.Name ?? winnerBot?.Name ?? "Unknown";

                // Send victory notification
                await map.BroadcastPacket(new ArenaAcceptServerPacket
                {
                    WinnerName = winnerName,
                    KillsCount = winnerArena.Kills,
                    KillerName = bot.Name,
                    VictimName = targetBot.Name
                });

                _logger.LogInformation("[ARENA] Match ended: {WinnerName} won with {Kills} kills",
                    winnerName, winnerArena.Kills);

                // DON'T clear arena or eject players here - let ArenaBotService handle that
                // Just return true to signal match ended
                return true;
            }
            
            return false; // Match continues
        }
        else
        {
            // Bot vs Player combat - handled by ArenaService.HandleArenaCombatAsync
            // This is triggered when a bot "attacks" a player, but actual combat is in ArenaService
            var targetPlayer = map.Players.FirstOrDefault(p => p.SessionId == targetId);
            if (targetPlayer?.Character is null)
            {
                _logger.LogWarning("[BOT] Bot {BotId} tried to attack player {TargetId} but player not found",
                    bot.Id, targetId);
                return false;
            }

            _logger.LogDebug("[BOT COMBAT] Bot {BotName} (ID: {BotId}) attacking player {PlayerName} (ID: {PlayerId})",
                bot.Name, bot.Id, targetPlayer.Character.Name, targetPlayer.SessionId);

            // Send attack animation
            await map.BroadcastPacket(new AttackPlayerServerPacket
            {
                PlayerId = bot.Id,
                Direction = bot.Direction
            });
            
            return false; // Bot vs player doesn't instantly end match
        }
    }

    public async Task IdleAnimationAsync(ArenaBotState bot, MapState map)
    {
        // Randomly choose an idle action
        var action = Random.Shared.Next(0, 3);

        switch (action)
        {
            case 0:
                // Change facing direction
                var directions = new[] { Direction.Down, Direction.Up, Direction.Left, Direction.Right };
                var newDirection = directions[Random.Shared.Next(directions.Length)];
                
                if (newDirection != bot.Direction)
                {
                    bot.Direction = newDirection;
                    
                    // Send face direction packet
                    await map.BroadcastPacket(new FacePlayerServerPacket
                    {
                        PlayerId = bot.Id,
                        Direction = newDirection
                    });

                    _logger.LogTrace("[BOT] Bot {BotName} (ID: {BotId}) changed direction to {Direction}",
                        bot.Name, bot.Id, newDirection);
                }
                break;

            case 1:
                // Emote action (looking around)
                _logger.LogTrace("[BOT] Bot {BotName} (ID: {BotId}) idle animation",
                    bot.Name, bot.Id);
                break;

            case 2:
                // Do nothing this tick
                break;
        }
    }

    #region Private Helpers

    private bool IsTileWalkable(MapState map, Coords coords)
    {
        // Check map bounds
        if (coords.X < 0 || coords.X >= map.Data.Width || coords.Y < 0 || coords.Y >= map.Data.Height)
        {
            return false;
        }

        // Use tile service to check walkability
        return _tileService.IsTileWalkable(map.Data, coords);
    }

    private bool IsTileOccupied(MapState map, Coords coords)
    {
        // Check for players
        if (map.Players.Any(p => p.Character != null && p.Character.X == coords.X && p.Character.Y == coords.Y))
        {
            return true;
        }

        // Check for other bots
        if (map.ArenaBots.Any(b => b.X == coords.X && b.Y == coords.Y))
        {
            return true;
        }

        // Check for NPCs
        if (map.Npcs.Any(n => !n.IsDead && n.X == coords.X && n.Y == coords.Y))
        {
            return true;
        }

        return false;
    }

    private Direction GetAlternateDirection(Direction primary, int dx, int dy)
    {
        // If primary was horizontal, try vertical; if primary was vertical, try horizontal
        return primary switch
        {
            Direction.Right or Direction.Left => dy > 0 ? Direction.Down : Direction.Up,
            Direction.Up or Direction.Down => dx > 0 ? Direction.Right : Direction.Left,
            _ => Direction.Down
        };
    }

    #endregion
}
