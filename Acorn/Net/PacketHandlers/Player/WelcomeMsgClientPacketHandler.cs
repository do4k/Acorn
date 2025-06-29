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
        _logger = logger;
        _newsTxt = File.ReadAllLines("Data/news.txt");
        _world = worldState;
    }

    public async Task HandleAsync(
        ConnectionHandler connectionHandler,
        WelcomeMsgClientPacket packet)
    {
        if (connectionHandler.CharacterController is null)
        {
            _logger.LogWarning("ConnectionHandler '{SessionId}' tried to enter the game without a character controller.", connectionHandler.SessionId);
            return;
        }
        
        connectionHandler.ClientState = ClientState.InGame;
        var map = _world.MapForId(connectionHandler.CharacterController.Data.Map);
        if (map is null)
        {
            return;
        }

        await map.NotifyEnter(connectionHandler);

        await connectionHandler.Send(new WelcomeReplyServerPacket
        {
            WelcomeCode = WelcomeCode.EnterGame,
            WelcomeCodeData = new WelcomeReplyServerPacket.WelcomeCodeDataEnterGame
            {
                Items = connectionHandler.CharacterController.GetItems().ToList(),
                News = new List<string> { " " }.Concat(_newsTxt.Concat(Enumerable.Range(0, 8 - _newsTxt.Length).Select(_ => ""))).ToList(),
                Weight = new Weight
                {
                    Current = 0,
                    Max = 100
                },
                Nearby = map.AsNearbyInfo()
            }
        });
    }

    public Task HandleAsync(ConnectionHandler connectionHandler, object packet)
    {
        return HandleAsync(connectionHandler, (WelcomeMsgClientPacket)packet);
    }
}