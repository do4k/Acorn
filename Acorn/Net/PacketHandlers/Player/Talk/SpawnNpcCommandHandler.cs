using Acorn.Database.Repository;
using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Net.PacketHandlers.Player.Talk;

public class SpawnNpcCommandHandler : ITalkHandler
{
    private readonly ILogger<SpawnNpcCommandHandler> _logger;
    private readonly IDataFileRepository _dataFiles;
    private readonly WorldState _world;

    public SpawnNpcCommandHandler(WorldState world, IDataFileRepository dataFiles,
        ILogger<SpawnNpcCommandHandler> logger)
    {
        _world = world;
        _dataFiles = dataFiles;
        _logger = logger;
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
            await playerState.ServerMessage("Usage: $[spawnnpc | snpc] <npc_id|npc_name>");
            return;
        }

        if (int.TryParse(args[0], out var npcId) is false)
        {
            await SpawnByName(playerState, args[0]);
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
        await playerState.ServerMessage($"Spawned NPC {enf.Name} ({npcId}).");
        await playerState.CurrentMap.BroadcastPacket(new NpcAgreeServerPacket
        {
            Npcs = playerState.CurrentMap.AsNpcMapInfo()
        });
    }

    private Task SpawnByName(PlayerState playerState, string name)
    {
        var npc = _dataFiles.Enf.Npcs.FirstOrDefault(x =>
            x.Name.Contains(name, StringComparison.CurrentCultureIgnoreCase));

        return npc == null ? playerState.ServerMessage($"NPC {name} not found.") : SpawnNpc(playerState, npc);
    }
}