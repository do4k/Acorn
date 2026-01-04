using Acorn.Database.Repository;
using Acorn.Net.Services;
using Acorn.World;
using Acorn.World.Npc;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Net.PacketHandlers.Player.Talk;

public class SpawnNpcCommandHandler : ITalkHandler
{
    /// <summary>
    ///     EO Protocol limit: NpcMapInfo uses a single byte to store count, max value is 252.
    ///     The SDK caps it at 252 for safety to prevent "Value 253 exceeds maximum of 252" errors.
    /// </summary>
    private const int MaxNpcsPerMap = 252;

    private readonly IDataFileRepository _dataFiles;

    private readonly ILogger<SpawnNpcCommandHandler> _logger;
    private readonly INotificationService _notifications;

    public SpawnNpcCommandHandler(IWorldQueries world, IDataFileRepository dataFiles,
        ILogger<SpawnNpcCommandHandler> logger, INotificationService notifications)
    {
        _dataFiles = dataFiles;
        _logger = logger;
        _notifications = notifications;
    }

    public bool CanHandle(string command)
    {
        return command.Equals("spawnnpc", StringComparison.InvariantCultureIgnoreCase)
               || command.Equals("snpc", StringComparison.InvariantCultureIgnoreCase);
    }

    public async Task HandleAsync(PlayerState playerState, string command, params string[] args)
    {
        if (args.Length < 1)
        {
            await _notifications.ServerAnnouncement(playerState, "Usage: $[spawnnpc | snpc] <npc_id|npc_name> [count]");
            return;
        }

        var count = 1;
        var inputArgs = args;

        // Check if the last argument is a number (spawn count)
        if (args.Length > 1 && int.TryParse(args[^1], out var parsedCount))
        {
            count = parsedCount;
            inputArgs = args[..^1]; // Remove the last argument
        }

        // Join all arguments to support multi-word NPC names
        var input = string.Join(" ", inputArgs);

        if (int.TryParse(input, out var npcId))
        {
            var npc = _dataFiles.Enf.GetNpc(npcId);
            if (npc is null)
            {
                await _notifications.ServerAnnouncement(playerState, $"NPC with ID {npcId} not found.");
                return;
            }

            await SpawnNpc(playerState, npc, count);
            return;
        }

        await SpawnByName(playerState, input, count);
    }

    private async Task SpawnNpc(PlayerState playerState, EnfRecord enf, int count = 1)
    {
        if (playerState.Character is null)
        {
            _logger.LogError("Character has not been initialised on connection");
            return;
        }

        if (playerState.CurrentMap is null)
        {
            return;
        }

        // Validate NPC count doesn't exceed protocol limit
        var currentNpcCount = playerState.CurrentMap.Npcs.Count;
        if (currentNpcCount + count > MaxNpcsPerMap)
        {
            var canSpawn = Math.Max(0, MaxNpcsPerMap - currentNpcCount);
            await _notifications.ServerAnnouncement(playerState,
                $"Cannot spawn {count} {enf.Name}s: Map has {currentNpcCount} NPCs. " +
                $"Protocol limit is {MaxNpcsPerMap}. Can only spawn {canSpawn} more.");
            return;
        }

        var npcId = _dataFiles.Enf.Npcs.FindIndex(x => enf.GetHashCode() == x.GetHashCode());

        for (var i = 0; i < count; i++)
        {
            var npc = new NpcState(enf)
            {
                Direction = playerState.Character.Direction,
                X = playerState.Character.X,
                Y = playerState.Character.Y,
                Hp = enf.Hp,
                Id = npcId + 1,
                IsAdminSpawned = true
            };

            playerState.CurrentMap.Npcs.Add(npc);
        }

        await _notifications.ServerAnnouncement(playerState,
            count == 1
                ? $"Spawned {enf.Name} ({npcId})."
                : $"Spawned {count} {enf.Name}s ({npcId}).");

        await playerState.CurrentMap.BroadcastPacket(new NpcAgreeServerPacket
        {
            Npcs = playerState.CurrentMap.AsNpcMapInfo()
        });
    }

    private async Task SpawnByName(PlayerState playerState, string name, int count = 1)
    {
        // Try exact match first
        var exactMatches = _dataFiles.Enf.FindByName(name);
        if (exactMatches.Count == 1)
        {
            await SpawnNpc(playerState, exactMatches[0].Npc, count);
            return;
        }

        if (exactMatches.Count > 1)
        {
            var ids = string.Join(", ", exactMatches.Select(x => x.Id));
            await _notifications.ServerAnnouncement(playerState,
                $"Multiple NPCs found with name \"{name}\": IDs {ids}");
            return;
        }

        // No exact match, try partial match
        var partialMatches = _dataFiles.Enf.SearchByName(name);
        if (partialMatches.Count == 1)
        {
            await SpawnNpc(playerState, partialMatches[0].Npc, count);
            return;
        }

        if (partialMatches.Count > 1)
        {
            var suggestions = string.Join(", ", partialMatches.Take(5).Select(x => $"{x.Npc.Name} ({x.Id})"));
            await _notifications.ServerAnnouncement(playerState, $"Multiple NPCs match \"{name}\": {suggestions}");
            return;
        }

        await _notifications.ServerAnnouncement(playerState, $"NPC \"{name}\" not found.");
    }
}