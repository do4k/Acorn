using Acorn.World.Services.Player;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Player;

/// <summary>
///     Walk/Spec is sent by the client when walking through another player.
///     eoserv registers it to the normal walk handler, so do the same.
/// </summary>
[RequiresCharacter]
internal class WalkSpecClientPacketHandler : IPacketHandler<WalkSpecClientPacket>
{
    private readonly IWalkService _walkService;

    public WalkSpecClientPacketHandler(IWalkService walkService)
    {
        _walkService = walkService;
    }

    public Task HandleAsync(PlayerState playerState,
        WalkSpecClientPacket packet)
    {
        return _walkService.WalkAsync(
            playerState,
            packet.WalkAction.Direction,
            packet.WalkAction.Timestamp,
            packet.WalkAction.Coords);
    }
}
