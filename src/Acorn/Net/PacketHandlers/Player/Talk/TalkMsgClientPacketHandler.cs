using Acorn.World;
using Acorn.World.Npc;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player.Talk;

internal class TalkMsgClientPacketHandler : IPacketHandler<TalkMsgClientPacket>
{
    private readonly IWorldQueries _world;

    public TalkMsgClientPacketHandler(IWorldQueries world)
    {
        _world = world;
    }

    public async Task HandleAsync(PlayerState playerState, TalkMsgClientPacket packet)
    {
        var message = new GlobalMessage(Guid.NewGuid(), packet.Message, playerState.Character?.Name ?? "Unknown", DateTime.UtcNow);
        _world.AddGlobalMessage(message);

        var broadcast = _world.GetGlobalChatListeners()
            .Where(x => x != playerState)
            .Select(x => x.Send(new TalkMsgServerPacket
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