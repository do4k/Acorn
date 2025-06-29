using Acorn.World;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player.Talk;

internal class TalkMsgClientPacketHandler : IPacketHandler<TalkMsgClientPacket>
{
    private readonly WorldState _world;

    public TalkMsgClientPacketHandler(WorldState world)
    {
        _world = world;
    }

    public async Task HandleAsync(ConnectionHandler connectionHandler, TalkMsgClientPacket packet)
    {
        var id = Guid.NewGuid();
        var message = new GlobalMessage(Guid.NewGuid(), packet.Message, connectionHandler.CharacterController?.Data.Name ?? "Unknown", DateTime.UtcNow);
        _world.GlobalMessages.TryAdd(id, message);

        var broadcast = _world.Players
            .Where(x => x.Value != connectionHandler && x.Value.IsListeningToGlobal)
            .Select(x => x.Value.Send(new TalkMsgServerPacket
            {
                Message = packet.Message,
                PlayerName = connectionHandler.CharacterController?.Data.Name!
            }));

        await Task.WhenAll(broadcast);

    }

    public Task HandleAsync(ConnectionHandler connectionHandler, object packet)
    {
        return HandleAsync(connectionHandler, (TalkMsgClientPacket)packet);
    }
}