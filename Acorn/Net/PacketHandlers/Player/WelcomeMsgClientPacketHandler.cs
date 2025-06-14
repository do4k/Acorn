using Acorn.Database.Repository;
using Acorn.Net.Models;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

internal class WelcomeMsgClientPacketHandler : IPacketHandler<WelcomeMsgClientPacket>
{
    private readonly IDataFileRepository _dataRepository;
    private readonly string[] _newsTxt;
    private readonly WorldState _world;

    public WelcomeMsgClientPacketHandler(
        IDataFileRepository dataRepository,
        WorldState worldState
    )
    {
        _newsTxt = File.ReadAllLines("Data/news.txt");
        _dataRepository = dataRepository;
        _world = worldState;
    }

    public async Task HandleAsync(PlayerConnection playerConnection,
        WelcomeMsgClientPacket packet)
    {
        playerConnection.ClientState = ClientState.InGame;
        var map = _world.MapFor(playerConnection);
        if (map is null)
        {
            return;
        }

        await map.Enter(playerConnection);

        await playerConnection.Send(new WelcomeReplyServerPacket
        {
            WelcomeCode = WelcomeCode.EnterGame,
            WelcomeCodeData = new WelcomeReplyServerPacket.WelcomeCodeDataEnterGame
            {
                Items = playerConnection.Character?.Items().ToList(),
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

    public Task HandleAsync(PlayerConnection playerConnection, object packet)
    {
        return HandleAsync(playerConnection, (WelcomeMsgClientPacket)packet);
    }
}