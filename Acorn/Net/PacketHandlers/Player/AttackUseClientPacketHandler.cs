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
    private DateTime _timeSinceLastAttack;
    private readonly ILogger<AttackUseClientPacketHandler> _logger;
    private readonly FormulaService _formulaService;

    public AttackUseClientPacketHandler(UtcNowDelegate now, ILogger<AttackUseClientPacketHandler> logger, FormulaService formulaService)
    {
        _now = now;
        _logger = logger;
        _formulaService = formulaService;
    }

    public async Task HandleAsync(ConnectionHandler connectionHandler, AttackUseClientPacket packet)
    {
        if ((_now() - _timeSinceLastAttack).TotalMilliseconds < 500)
        {
            return;
        }

        if (connectionHandler.CurrentMap is null)
        {
            return;
        }

        if (connectionHandler.CharacterController is not null)
        {
            var nextCoords = connectionHandler.CharacterController.NextCoords();
            var target = connectionHandler.CurrentMap.Npcs.FirstOrDefault(x =>
                x.X == nextCoords.X && x.Y == nextCoords.Y &&
                x.Data.Type is NpcType.Aggressive or NpcType.Passive);

            if (target is null)
            {
                return;
            }

            var damage = _formulaService.CalculateDamage(connectionHandler.CharacterController.Data, target.Data);
            target.Hp -= damage;
            target.Hp = Math.Max(target.Hp, 0);
            await connectionHandler.CurrentMap.BroadcastPacket(new NpcReplyServerPacket
            {
                PlayerId = connectionHandler.SessionId,
                PlayerDirection = connectionHandler.CharacterController.Data.Direction,
                NpcIndex = connectionHandler.CurrentMap.Npcs.ToList().IndexOf(target),
                Damage = damage,
                HpPercentage = (int)Math.Max((double)target.Hp / target.Data.Hp * 100, 0),
                KillStealProtection = NpcKillStealProtectionState.Unprotected,
            });

            if (target.Hp == 0)
            {
                connectionHandler.CharacterController.GainExp(target.Data.Experience);
            }
        }

        await connectionHandler.CurrentMap.BroadcastPacket(new AttackPlayerServerPacket
        {
            Direction = connectionHandler.CharacterController?.Data.Direction ?? Direction.Down,
            PlayerId = connectionHandler.SessionId
        }, except: connectionHandler);

        _timeSinceLastAttack = DateTime.UtcNow;
    }

    public Task HandleAsync(ConnectionHandler connectionHandler, object packet)
    {
        return HandleAsync(connectionHandler, (AttackUseClientPacket)packet);
    }
}