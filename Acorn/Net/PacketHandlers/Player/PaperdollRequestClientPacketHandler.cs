using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Player;

public class PaperdollRequestClientPacketHandler : IPacketHandler<PaperdollRequestClientPacket>
{
    public Task HandleAsync(PlayerState playerState, PaperdollRequestClientPacket packet)
    {
        throw new NotImplementedException();
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
        => HandleAsync(playerState, (PaperdollRequestClientPacket)packet);
}