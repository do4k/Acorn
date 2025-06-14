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

    public async Task HandleAsync(PlayerConnection playerConnection, string command, params string[] args)
    {
        if (args.Length < 1)
        {
            await playerConnection.Send(new TalkServerServerPacket
            {
                Message = "Usage: $spawnnpc <npc_id|npc_name>"
            });
            return;
        }

        if (int.TryParse(args[0], out var npcId) is false)
        {
            await SpawnByName(playerConnection, args[0]);
            return;
        }

        var npc = _dataFiles.Enf.GetNpc(npcId);
        if (npc is null)
        {
            return;
        }

        await SpawnNpc(playerConnection, npc);
    }

    private async Task SpawnNpc(PlayerConnection playerConnection, EnfRecord enf)
    {
        if (playerConnection.Character is null)
        {
            _logger.LogError("Character has not been initialised on connection");
            return;
        }

        var npcId = _dataFiles.Enf.Npcs.FindIndex(x => enf.GetHashCode() == x.GetHashCode());

        var npc = new NpcState(enf)
        {
            Direction = playerConnection.Character.Direction,
            X = playerConnection.Character.X,
            Y = playerConnection.Character.Y,
            Hp = enf.Hp,
            Id = npcId + 1
        };

        var map = _world.MapFor(playerConnection);
        if (map is null)
        {
            return;
        }

        map.Npcs.Add(npc);
        await playerConnection.ServerMessage($"Spawned NPC {enf.Name} ({npcId}).");
        await map.BroadcastPacket(new NpcAgreeServerPacket
        {
            Npcs = map.AsNpcMapInfo()
        });
    }

    private Task SpawnByName(PlayerConnection playerConnection, string name)
    {
        var npc = _dataFiles.Enf.Npcs.FirstOrDefault(x =>
            x.Name.Contains(name, StringComparison.CurrentCultureIgnoreCase));

        return npc == null ? playerConnection.ServerMessage($"NPC {name} not found.") : SpawnNpc(playerConnection, npc);
    }
}