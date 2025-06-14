using System.Collections.Concurrent;
using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Net;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.World;


public class MapState
{
    public MapState(MapWithId data, IDataFileRepository dataRepository, ILogger<WorldState> logger)
    {
        Id = data.Id;
        Data = data.Map;
        var mapNpcs = data.Map.Npcs.SelectMany(mapNpc => Enumerable.Range(0, mapNpc.Amount).Select(_ => mapNpc));
        foreach (var npc in mapNpcs)
        {
            var npcData = dataRepository.Enf.GetNpc(npc.Id);
            if (npcData is null)
            {
                logger.LogError("Could not find npc with id {NpcId}", npc.Id);
                continue;
            }
            var npcState = new NpcState(npcData)
            {
                Direction = Direction.Down,
                X = npc.Coords.X,
                Y = npc.Coords.Y,
                Hp = npcData!.Hp,
                Id = npc.Id
            };
            Npcs.Add(npcState);
        }
    }

    public int Id { get; set; }
    public Emf Data { get; set; }

    public ConcurrentBag<NpcState> Npcs { get; set; } = new();
    public ConcurrentBag<PlayerConnection> Players { get; set; } = new();

    public bool HasPlayer(PlayerConnection player)
    {
        return Players.Contains(player);
    }

    public IEnumerable<PlayerConnection> PlayersExcept(PlayerConnection playerConnection)
    {
        return Players.Where(x => x != playerConnection);
    }

    public async Task BroadcastPacket(IPacket packet, PlayerConnection? except = null)
    {
        var otherPlayers = Players.Where(x => except is null || x != except)
            .ToList();

        var broadcast = otherPlayers
            .Select(async otherPlayer => await otherPlayer.Send(packet));

        await Task.WhenAll(broadcast);
    }

    public NearbyInfo AsNearbyInfo(PlayerConnection? except = null, WarpEffect warpEffect = WarpEffect.None)
    {
        return new NearbyInfo
        {
            Characters = Players
                .Where(x => x.Character is not null)
                .Where(x => except == null || x != except)
                .Select(x => x.Character?.AsCharacterMapInfo(x.SessionId, warpEffect))
                .ToList(),
            Items = [],
            Npcs = AsNpcMapInfo()
        };
    }

    public List<NpcMapInfo> AsNpcMapInfo()
    {
        return Npcs.Select((x, i) => x.AsNpcMapInfo(i)).ToList();
    }

    public async Task Enter(PlayerConnection player, WarpEffect warpEffect = WarpEffect.None)
    {
        if (player.Character is null)
        {
            return;
        }

        player.Character.Map = Id;

        if (!Players.Contains(player))
        {
            Players.Add(player);
        }

        await BroadcastPacket(new PlayersAgreeServerPacket
        {
            Nearby = AsNearbyInfo(null, warpEffect)
        }, player);
    }

    public async Task Leave(PlayerConnection player, WarpEffect warpEffect = WarpEffect.None)
    {
        Players = new ConcurrentBag<PlayerConnection>(Players.Where(p => p != player));

        await BroadcastPacket(new PlayersRemoveServerPacket
        {
            PlayerId = player.SessionId
        });

        await BroadcastPacket(new AvatarRemoveServerPacket
        {
            PlayerId = player.SessionId,
            WarpEffect = warpEffect
        });
    }

    public void Tick()
    {
        foreach (var player in Players)
        {
            var before = player.Character?.Hp;
            var after = player.Character?.Recover(15);
        }
    }
}