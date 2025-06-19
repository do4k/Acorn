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

    public WelcomeMsgClientPacketHandler(
        WorldState worldState
    )
    {
        _newsTxt = File.ReadAllLines("Data/news.txt");
        _world = worldState;
    }

    public async Task HandleAsync(
        PlayerState playerState,
        WelcomeMsgClientPacket packet)
    {
        playerState.ClientState = ClientState.InGame;
        var map = _world.MapForId(playerState.Character?.Map ?? -1);
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
}