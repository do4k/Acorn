using Acorn.World;
using Acorn.World.Npc;
using Moffat.EndlessOnline.SDK.Protocol.Net;
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

    public async Task HandleAsync(PlayerState playerState, TalkMsgClientPacket packet)
    {
        var id = Guid.NewGuid();
        var message = new GlobalMessage(Guid.NewGuid(), packet.Message, playerState.Character?.Name ?? "Unknown", DateTime.UtcNow);
        _world.GlobalMessages.TryAdd(id, message);

        var broadcast = _world.Players
            .Where(x => x.Value != playerState && x.Value.IsListeningToGlobal)
            .Select(x => x.Value.Send(new TalkMsgServerPacket
            {
                Message = packet.Message,
                PlayerName = playerState.Character?.Name!
            }));

        await Task.WhenAll(broadcast);

    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (TalkMsgClientPacket)packet);
    }
}