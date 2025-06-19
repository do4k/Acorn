using Acorn.Database.Repository;
using Acorn.Net.Models;
using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

internal class WelcomeMsgClientPacketHandler : IPacketHandler<WelcomeMsgClientPacket>
{
    private readonly string[] _newsTxt;
    private readonly WorldState _world;
    private readonly ILogger<WelcomeMsgClientPacketHandler> _logger;

    public WelcomeMsgClientPacketHandler(
        WorldState worldState,
        ILogger<WelcomeMsgClientPacketHandler> logger
    )
    {
        _newsTxt = File.ReadAllLines("Data/news.txt");
        _world = worldState;
        _logger = logger;
    }

    public async Task HandleAsync(
        PlayerState playerState,
        WelcomeMsgClientPacket packet)
    {
        playerState.ClientState = ClientState.InGame;
        var map = GetMapForPlayerAsync(playerState);
        if (map is null)
        {
            return;
        }

        await map.NotifyEnter(playerState);

        await playerState.Send(new WelcomeReplyServerPacket
        {
            WelcomeCode = WelcomeCode.EnterGame,
            WelcomeCodeData = new WelcomeReplyServerPacket.WelcomeCodeDataEnterGame
            {
                Items = playerState.Character?.Items().ToList(),
                News = new List<string> { " " }
                    .Concat(_newsTxt.Concat(Enumerable.Range(0, 8 - _newsTxt.Length).Select(_ => ""))).ToList(),
                Weight = new Weight
                {
                    Current = 0,
                    Max = 100
                },
                Nearby = map.AsNearbyInfo()
            }
        });
    }

    public Task HandleAsync(PlayerState playerState, object packet)
    {
        return HandleAsync(playerState, (WelcomeMsgClientPacket)packet);
    }

    private MapState? GetMapForPlayerAsync(PlayerState player)
    {
        var exists = _world.Maps.TryGetValue(player.Character?.Map ?? -1, out var map);
        if (exists is true && map is not null)
        {
            return map;
        }

        _logger.LogWarning("Player {CharacterName} ({SessionId}) attempted to access non-existent map {MapId}",
            player.Character?.Name, player.SessionId, player.Character?.Map);
        return null;
    }
}