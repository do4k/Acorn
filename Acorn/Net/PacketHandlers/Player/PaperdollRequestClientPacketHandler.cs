using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Player;

public class PaperdollRequestClientPacketHandler : IPacketHandler<PaperdollRequestClientPacket>
{
    public Task HandleAsync(ConnectionHandler connectionHandler, PaperdollRequestClientPacket packet)
    {
        throw new NotImplementedException();
    }

    public Task HandleAsync(ConnectionHandler connectionHandler, object packet)
        => HandleAsync(connectionHandler, (PaperdollRequestClientPacket)packet);
}