using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Player;

public class WalkAdminClientPacketHandler : IPacketHandler<WalkAdminClientPacket>
{
    private readonly IPacketHandler<WalkPlayerClientPacket> _playerWalkHandler;

    public WalkAdminClientPacketHandler(IPacketHandler<WalkPlayerClientPacket> playerWalkHandler)
    {
        _playerWalkHandler = playerWalkHandler;
    }

    public Task HandleAsync(PlayerState playerState,
        WalkAdminClientPacket packet)
    {
        return _playerWalkHandler.HandleAsync(playerState, new WalkPlayerClientPacket
        {
            WalkAction = packet.WalkAction
        });
    }

    public Task HandleAsync(PlayerState playerState, object packet)
    {
        return HandleAsync(playerState, (WalkAdminClientPacket)packet);
    }
}