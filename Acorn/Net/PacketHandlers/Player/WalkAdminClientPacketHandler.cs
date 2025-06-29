using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Player;

public class WalkAdminClientPacketHandler : IPacketHandler<WalkAdminClientPacket>
{
    private readonly IPacketHandler<WalkPlayerClientPacket> _playerWalkHandler;

    public WalkAdminClientPacketHandler(IPacketHandler<WalkPlayerClientPacket> playerWalkHandler)
    {
        _playerWalkHandler = playerWalkHandler;
    }

    public Task HandleAsync(ConnectionHandler connectionHandler,
        WalkAdminClientPacket packet)
    {
        return _playerWalkHandler.HandleAsync(connectionHandler, new WalkPlayerClientPacket
        {
            WalkAction = packet.WalkAction
        });
    }

    public Task HandleAsync(ConnectionHandler connectionHandler, object packet)
    {
        return HandleAsync(connectionHandler, (WalkAdminClientPacket)packet);
    }
}