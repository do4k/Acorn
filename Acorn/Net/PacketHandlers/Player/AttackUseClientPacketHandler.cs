using Acorn.Extensions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using OneOf;
using OneOf.Types;

namespace Acorn.Net.PacketHandlers.Player;

internal class AttackUseClientPacketHandler : IPacketHandler<AttackUseClientPacket>
{
    private readonly UtcNowDelegate _now;
    private readonly WorldState _world;
    private DateTime _timeSinceLastAttack;

    public AttackUseClientPacketHandler(WorldState world, UtcNowDelegate now)
    {
        _world = world;
        _now = now;
    }

    public Task<OneOf<Success, Error>> HandleAsync(PlayerConnection playerConnection, AttackUseClientPacket packet)
    {
        if ((_now() - _timeSinceLastAttack).TotalMilliseconds < 500)
        {
            return Task.FromResult<OneOf<Success, Error>>(new Success());
        }

        var map = _world.MapFor(playerConnection);

        map.Players.Where(x => x != playerConnection).ToList().ForEach(async otherPlayer =>
        {
            await otherPlayer.Send(new AttackPlayerServerPacket
            {
                Direction = playerConnection.Character?.Direction ?? Direction.Down,
                PlayerId = playerConnection.SessionId
            });
        });

        _timeSinceLastAttack = DateTime.UtcNow;
        return Task.FromResult<OneOf<Success, Error>>(new Success());
    }

    public Task<OneOf<Success, Error>> HandleAsync(PlayerConnection playerConnection, object packet)
    {
        return HandleAsync(playerConnection, (AttackUseClientPacket)packet);
    }
}