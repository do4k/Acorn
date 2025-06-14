using Acorn.Extensions;
using Acorn.World;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Net.PacketHandlers.Player;

internal class AttackUseClientPacketHandler : IPacketHandler<AttackUseClientPacket>
{
    private readonly UtcNowDelegate _now;
    private readonly WorldState _world;
    private DateTime _timeSinceLastAttack;
    private readonly ILogger<AttackUseClientPacketHandler> _logger;
    private readonly FormulaService _formulaService;

    public AttackUseClientPacketHandler(WorldState world, UtcNowDelegate now, ILogger<AttackUseClientPacketHandler> logger, FormulaService formulaService)
    {
        _world = world;
        _now = now;
        _logger = logger;
        _formulaService = formulaService;
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

        if (playerConnection.Character is not null)
        {
            var nextCoords = playerConnection.Character.NextCoords();
            var target = map.Npcs.FirstOrDefault(x => 
                x.X == nextCoords.X && x.Y == nextCoords.Y && 
                x.Data.Type is NpcType.Aggressive or NpcType.Passive);
            
            if (target is null)
            {
                return;
            }
            
            var damage = _formulaService.CalculateDamage(playerConnection.Character, target.Data);
            target.Hp -= damage;
            target.Hp = Math.Max(target.Hp, 0);
            await map.BroadcastPacket(new NpcReplyServerPacket
            {
                PlayerId = playerConnection.SessionId,
                PlayerDirection = playerConnection.Character.Direction,
                NpcIndex = map.Npcs.ToList().IndexOf(target),
                Damage = damage,
                HpPercentage = (int)Math.Max((double)target.Hp / target.Data.Hp * 100, 0),
                KillStealProtection = NpcKillStealProtectionState.Unprotected,
            });

            if (target.Hp == 0)
            {
                playerConnection.Character.Exp += target.Data.Experience;
            }
        }

        await map.BroadcastPacket(new AttackPlayerServerPacket
            {
                Direction = playerConnection.Character?.Direction ?? Direction.Down,
                PlayerId = playerConnection.SessionId
            }, except: playerConnection);

        _timeSinceLastAttack = DateTime.UtcNow;
    }

    public Task HandleAsync(PlayerConnection playerConnection, object packet)
    {
        return HandleAsync(playerConnection, (AttackUseClientPacket)packet);
    }
}