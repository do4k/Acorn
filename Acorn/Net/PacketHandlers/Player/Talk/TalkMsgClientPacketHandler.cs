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

    public async Task HandleAsync(PlayerConnection playerConnection, TalkMsgClientPacket packet)
    {
        var id = Guid.NewGuid();
        var message = new GlobalMessage(Guid.NewGuid(), packet.Message, playerConnection.Character?.Name ?? "Unknown", DateTime.UtcNow);
        _world.GlobalMessages.TryAdd(id, message);

        var broadcast = _world.Players
            .Where(x => x.Value != playerConnection && x.Value.IsListeningToGlobal)
            .Select(x => x.Value.Send(new TalkMsgServerPacket
            {
                Message = packet.Message,
                PlayerName = playerConnection.Character?.Name!
            }));

        await Task.WhenAll(broadcast);

    }

    public Task HandleAsync(PlayerConnection playerConnection, object packet)
    {
        return HandleAsync(playerConnection, (TalkMsgClientPacket)packet);
    }
}