using Acorn.Net;
using Acorn.Options;
using Acorn.World.Map;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.World.Services.Arena;

public interface IArenaService
{
    bool IsArenaEnabled { get; }
    int ArenaMapId { get; }

    Task JoinArenaAsync(PlayerState player);
    Task LeaveArenaAsync(PlayerState player);
    Task HandleArenaAttackAsync(PlayerState attacker, PlayerState target);
    Task ProcessArenaDeathAsync(PlayerState deadPlayer);
}

public class ArenaService : IArenaService
{
    private readonly ArenaOptions _options;
    private readonly ILogger<ArenaService> _logger;
    private readonly WorldState _worldState;

    public bool IsArenaEnabled => _options.Enabled;
    public int ArenaMapId => _options.ArenaMapId;

    public ArenaService(
        IOptions<ArenaOptions> options,
        ILogger<ArenaService> logger,
        WorldState worldState)
    {
        _options = options.Value;
        _logger = logger;
        _worldState = worldState;
    }

    public async Task JoinArenaAsync(PlayerState player)
    {
        if (!IsArenaEnabled)
        {
            _logger.LogWarning("Arena is not enabled, player {SessionId} cannot join", player.SessionId);
            return;
        }

        var arenaMap = _worldState.MapForId(ArenaMapId);
        if (arenaMap == null)
        {
            _logger.LogWarning("Arena map {MapId} not found", ArenaMapId);
            return;
        }

        if (arenaMap.TryJoinArenaQueue(player.SessionId))
        {
            _logger.LogInformation("Player {SessionId} joined arena queue", player.SessionId);
            
            // Notify player they joined the queue
            await player.Send(new ArenaReplyServerPacket
            {
                State = ArenaState.Queued,
                Position = 0 // TODO: Calculate actual position
            });
        }
    }

    public async Task LeaveArenaAsync(PlayerState player)
    {
        if (!IsArenaEnabled)
        {
            return;
        }

        var arenaMap = _worldState.MapForId(ArenaMapId);
        if (arenaMap == null)
        {
            return;
        }

        arenaMap.LeaveArenaQueue(player.SessionId);
        arenaMap.RemoveFromArena(player.SessionId);

        _logger.LogInformation("Player {SessionId} left arena", player.SessionId);
    }

    public async Task HandleArenaAttackAsync(PlayerState attacker, PlayerState target)
    {
        var arenaMap = _worldState.MapForId(ArenaMapId);
        if (arenaMap == null || !arenaMap.IsArenaMap)
        {
            return;
        }

        var attackerArenaPlayer = arenaMap.ArenaPlayers.FirstOrDefault(p => p.SessionId == attacker.SessionId);
        var targetArenaPlayer = arenaMap.ArenaPlayers.FirstOrDefault(p => p.SessionId == target.SessionId);

        if (attackerArenaPlayer == null || targetArenaPlayer == null)
        {
            return;
        }

        // Mark target as dead in arena
        targetArenaPlayer.IsDead = true;
        attackerArenaPlayer.Kills++;

        _logger.LogInformation("Arena: {Attacker} killed {Target}. Kills: {KillCount}",
            attacker.SessionId, target.SessionId, attackerArenaPlayer.Kills);

        // Warp target out of arena
        await ProcessArenaDeathAsync(target);

        // Check win condition
        if (_options.KillsToWin > 0 && attackerArenaPlayer.Kills >= _options.KillsToWin)
        {
            await NotifyArenaWinAsync(attacker, attackerArenaPlayer.Kills);
            // End the arena round
            arenaMap.ArenaPlayers.Clear();
        }
    }

    public async Task ProcessArenaDeathAsync(PlayerState deadPlayer)
    {
        var arenaMap = _worldState.MapForId(ArenaMapId);
        if (arenaMap == null)
        {
            return;
        }

        // Remove from arena
        arenaMap.RemoveFromArena(deadPlayer.SessionId);

        // Warp player out (to their home or rescue location)
        // For now, warp to map 1, spawn point
        if (deadPlayer.Character != null)
        {
            var homeMap = _worldState.MapForId(deadPlayer.Character.HomeMap);
            if (homeMap != null)
            {
                await _worldState.Players[deadPlayer.Account!.Id]
                    .CurrentMap!
                    .NotifyLeave(deadPlayer);
                
                await homeMap.NotifyEnter(deadPlayer);
            }
        }

        // Notify player they died in arena
        await deadPlayer.Send(new ArenaReplyServerPacket
        {
            State = ArenaState.Dead
        });
    }

    private async Task NotifyArenaWinAsync(PlayerState winner, int kills)
    {
        _logger.LogInformation("Player {SessionId} won the arena with {Kills} kills", winner.SessionId, kills);

        await winner.Send(new ArenaReplyServerPacket
        {
            State = ArenaState.Won,
            Kills = kills
        });
    }
}
