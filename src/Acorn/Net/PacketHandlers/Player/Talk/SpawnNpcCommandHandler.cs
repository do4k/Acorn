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
    private readonly ILogger<SpawnNpcCommandHandler> _logger;
    private readonly IDataFileRepository _dataFiles;
    private readonly IWorldQueries _world;
    private readonly INotificationService _notifications;

    public SpawnNpcCommandHandler(IWorldQueries world, IDataFileRepository dataFiles,
        ILogger<SpawnNpcCommandHandler> logger, INotificationService notifications)
    {
        _world = world;
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
            await _notifications.SystemMessage(playerState, "Usage: $[spawnnpc | snpc] <npc_id|npc_name>");
            return;
        }

        // Join all arguments to support multi-word NPC names
        var input = string.Join(" ", args);

        if (int.TryParse(input, out var npcId) is false)
        {
            await SpawnByName(playerState, input);
            return;
        }

        var npc = _dataFiles.Enf.GetNpc(npcId);
        if (npc is null)
        {
            return;
        }

        await SpawnNpc(playerState, npc);
    }

    private async Task SpawnNpc(PlayerState playerState, EnfRecord enf)
    {
        if (playerState.Character is null)
        {
            _logger.LogError("Character has not been initialised on connection");
            return;
        }

        var npcId = _dataFiles.Enf.Npcs.FindIndex(x => enf.GetHashCode() == x.GetHashCode());

        var npc = new NpcState(enf)
        {
            Direction = playerState.Character.Direction,
            X = playerState.Character.X,
            Y = playerState.Character.Y,
            Hp = enf.Hp,
            Id = npcId + 1
        };

        if (playerState.CurrentMap is null)
        {
            return;
        }

        playerState.CurrentMap.Npcs.Add(npc);
        await _notifications.SystemMessage(playerState, $"Spawned Npc {enf.Name} ({npcId}).");
        await playerState.CurrentMap.BroadcastPacket(new NpcAgreeServerPacket
        {
            Npcs = playerState.CurrentMap.AsNpcMapInfo()
        });
    }

    private async Task SpawnByName(PlayerState playerState, string name)
    {
        // Try exact match first
        var exactMatches = _dataFiles.Enf.FindByName(name);
        if (exactMatches.Count == 1)
        {
            await SpawnNpc(playerState, exactMatches[0].Npc);
            return;
        }

        if (exactMatches.Count > 1)
        {
            var ids = string.Join(", ", exactMatches.Select(x => x.Id));
            await _notifications.SystemMessage(playerState, $"Multiple NPCs found with name \"{name}\": IDs {ids}");
            return;
        }

        // No exact match, try partial match
        var partialMatches = _dataFiles.Enf.SearchByName(name);
        if (partialMatches.Count == 1)
        {
            await SpawnNpc(playerState, partialMatches[0].Npc);
            return;
        }

        if (partialMatches.Count > 1)
        {
            var suggestions = string.Join(", ", partialMatches.Take(5).Select(x => $"{x.Npc.Name} ({x.Id})"));
            await _notifications.SystemMessage(playerState, $"Multiple NPCs match \"{name}\": {suggestions}");
            return;
        }

        await _notifications.SystemMessage(playerState, $"NPC \"{name}\" not found.");
    }
}