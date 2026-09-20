using Acorn.World;
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

        // The welcome is per-connection, not per tab-open, so it is not repeated when
        // the player switches away from and back to the global tab.
        if (!playerState.HasReceivedGlobalWelcome)
        {
            playerState.HasReceivedGlobalWelcome = true;
            var welcome = GlobalMessage.Welcome();
            await playerState.Send(new TalkMsgServerPacket
            {
                Message = welcome.Message,
                PlayerName = welcome.Author
            });
        }

        // Replay only messages that arrived since the last one this player received,
        // in chronological order, so reopening the tab catches up without duplicating.
        var missed = _world.GetRecentGlobalMessages()
            .Where(x => x.Sequence > playerState.LastGlobalMessageSequence)
            .OrderBy(x => x.Sequence)
            .ToList();

        foreach (var message in missed)
        {
            playerState.LastGlobalMessageSequence = message.Sequence;
            await playerState.Send(new TalkMsgServerPacket
            {
                Message = message.Message,
                PlayerName = message.Author
            });
        }
    }

}