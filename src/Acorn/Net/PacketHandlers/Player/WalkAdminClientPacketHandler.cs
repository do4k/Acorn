using Acorn.World.Services.Player;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;

namespace Acorn.Net.PacketHandlers.Player;

[RequiresCharacter]
public class WalkAdminClientPacketHandler : IPacketHandler<WalkAdminClientPacket>
{
    // Matches eoserv's `nowall` admin level (ADMIN_GUARDIAN).
    private const AdminLevel RequiredAdminLevel = AdminLevel.Guardian;

    private readonly ILogger<WalkAdminClientPacketHandler> _logger;
    private readonly IWalkService _walkService;

    public WalkAdminClientPacketHandler(
        IWalkService walkService,
        ILogger<WalkAdminClientPacketHandler> logger)
    {
        _walkService = walkService;
        _logger = logger;
    }

    public Task HandleAsync(PlayerState playerState,
        WalkAdminClientPacket packet)
    {
        if (playerState.Character is null || playerState.Character.Admin < RequiredAdminLevel)
        {
            _logger.LogWarning(
                "Player {SessionId} attempted an admin walk without sufficient privileges",
                playerState.SessionId);
            return Task.CompletedTask;
        }

        return _walkService.WalkAsync(
            playerState,
            packet.WalkAction.Direction,
            packet.WalkAction.Timestamp,
            packet.WalkAction.Coords,
            admin: true);
    }
}
