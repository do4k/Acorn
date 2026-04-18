using Acorn.Extensions;
using Acorn.Infrastructure.Telemetry;
using Acorn.Net;
using Acorn.Net.Models;
using Acorn.Net.PacketHandlers;
using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

internal class WelcomeMsgClientPacketHandler : IPacketHandler<WelcomeMsgClientPacket>
{
    private readonly string[] _newsTxt;
    private readonly IWorldQueries _world;
    private readonly ILogger<WelcomeMsgClientPacketHandler> _logger;

    public WelcomeMsgClientPacketHandler(
        IWorldQueries worldState,
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
        var map = _world.FindMap(playerState.Character?.Map ?? -1);
        if (map is null)
        {
            return;
        }

        await map.NotifyEnter(playerState);
        _logger.PlayerEnteredWorld(playerState.Character?.Name ?? "unknown", playerState.SessionId, map.Id);

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

}