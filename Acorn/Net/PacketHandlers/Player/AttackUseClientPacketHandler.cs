using Acorn.Extensions;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Net.PacketHandlers.Player;

internal class AttackUseClientPacketHandler : IPacketHandler<AttackUseClientPacket>
{
    private readonly UtcNowDelegate _now;
    private readonly WorldState _world;
    private DateTime _timeSinceLastAttack;
    private readonly ILogger<AttackUseClientPacketHandler> _logger;

    public AttackUseClientPacketHandler(WorldState world, UtcNowDelegate now, ILogger<AttackUseClientPacketHandler> logger)
    {
        _world = world;
        _now = now;
        _logger = logger;
    }

    public async Task HandleAsync(PlayerConnection playerConnection, AttackUseClientPacket packet)
    {
        if ((_now() - _timeSinceLastAttack).TotalMilliseconds < 500)
        {
            return;
        }

        var map = _world.MapFor(playerConnection);
        if (map is null)
        {
            return;
        }
        await map.Players.Where(x => x != playerConnection)
            .ToList()
            .ToAsyncEnumerable()
            .ForEachAsync(async otherPlayer => await otherPlayer.Send(new AttackPlayerServerPacket
            {
                Direction = playerConnection.Character?.Direction ?? Direction.Down,
                PlayerId = playerConnection.SessionId
            }));

        _timeSinceLastAttack = DateTime.UtcNow;
    }

    public Task HandleAsync(PlayerConnection playerConnection, object packet)
    {
        return HandleAsync(playerConnection, (AttackUseClientPacket)packet);
    }
}