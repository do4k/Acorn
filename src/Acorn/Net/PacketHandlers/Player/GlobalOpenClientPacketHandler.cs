using Acorn.World;
using Acorn.World.Npc;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

public class GlobalOpenClientPacketHandler : IPacketHandler<GlobalOpenClientPacket>
{
    private readonly IWorldQueries _world;

    public GlobalOpenClientPacketHandler(IWorldQueries world)
    {
        _world = world;
    }

    public async Task HandleAsync(PlayerState playerState, GlobalOpenClientPacket packet)
    {
        playerState.IsListeningToGlobal = true;
        var messages = new[] { GlobalMessage.Welcome() }
            .Concat(_world.GetRecentGlobalMessages(10));

        foreach (var message in messages)
        {
            await playerState.Send(new TalkMsgServerPacket
            {
                Message = message.Message,
                PlayerName = message.Author
            });
        }
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (GlobalOpenClientPacket)packet);
    }
}