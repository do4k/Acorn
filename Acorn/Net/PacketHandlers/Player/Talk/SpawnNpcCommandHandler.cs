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

    public async Task HandleAsync(ConnectionHandler connectionHandler, string command, params string[] args)
    {
        if (args.Length < 1)
        {
            await connectionHandler.ServerMessage("Usage: $[spawnnpc | snpc] <npc_id|npc_name>");
            return;
        }

        if (int.TryParse(args[0], out var npcId) is false)
        {
            await SpawnByName(connectionHandler, args[0]);
            return;
        }

        var npc = _dataFiles.Enf.GetNpc(npcId);
        if (npc is null)
        {
            return;
        }

        await SpawnNpc(connectionHandler, npc);
    }

    private async Task SpawnNpc(ConnectionHandler connectionHandler, EnfRecord enf)
    {
        if (connectionHandler.CharacterController is null)
        {
            _logger.LogError("Character has not been initialised on connection");
            return;
        }

        var npcId = _dataFiles.Enf.Npcs.FindIndex(x => enf.GetHashCode() == x.GetHashCode());

        var npc = new NpcState(enf)
        {
            Direction = connectionHandler.CharacterController.Data.Direction,
            X = connectionHandler.CharacterController.Data.X,
            Y = connectionHandler.CharacterController.Data.Y,
            Hp = enf.Hp,
            Id = npcId + 1
        };

        if (connectionHandler.CurrentMap is null)
        {
            return;
        }

        connectionHandler.CurrentMap.Npcs.Add(npc);
        await connectionHandler.ServerMessage($"Spawned NPC {enf.Name} ({npcId}).");
        await connectionHandler.CurrentMap.BroadcastPacket(new NpcAgreeServerPacket
        {
            Npcs = connectionHandler.CurrentMap.AsNpcMapInfo()
        });
    }

    private Task SpawnByName(ConnectionHandler connectionHandler, string name)
    {
        var npc = _dataFiles.Enf.Npcs.FirstOrDefault(x =>
            x.Name.Contains(name, StringComparison.CurrentCultureIgnoreCase));

        return npc == null ? connectionHandler.ServerMessage($"NPC {name} not found.") : SpawnNpc(connectionHandler, npc);
    }
}