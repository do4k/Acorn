using Acorn.World;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

public class GlobalOpenClientPacketHandler : IPacketHandler<GlobalOpenClientPacket>
{
    private readonly WorldState _world;

    public GlobalOpenClientPacketHandler(WorldState world)
    {
        _world = world;
    }

    public Task HandleAsync(ConnectionHandler connectionHandler, GlobalOpenClientPacket packet)
    {
        connectionHandler.IsListeningToGlobal = true;
        new[] { GlobalMessage.Welcome() }
            .Concat(_world.GlobalMessages.Values.OrderByDescending(x => x.CreatedAt).Take(10))
            .ToAsyncEnumerable()
            .ForEachAsync(async x =>
            {
                await connectionHandler.Send(new TalkMsgServerPacket
                {
                    Message = x.Message,
                    PlayerName = x.Author
                });
            });

        return Task.CompletedTask;
    }

    public Task HandleAsync(ConnectionHandler connectionHandler, object packet)
    {
        return HandleAsync(connectionHandler, (GlobalOpenClientPacket)packet);
    }
}