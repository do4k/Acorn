using Acorn.Extensions;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
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

    public async Task HandleAsync(PlayerState playerState, AttackUseClientPacket packet)
    {
        if ((_now() - _timeSinceLastAttack).TotalMilliseconds < 500)
        {
            return;
        }

        if (playerState.CurrentMap is null)
        {
            return;
        }

        if (playerState.Character is not null)
        {
            var nextCoords = playerState.Character.NextCoords();
            var target = playerState.CurrentMap.Npcs.FirstOrDefault(x =>
                x.X == nextCoords.X && x.Y == nextCoords.Y &&
                x.Data.Type is NpcType.Aggressive or NpcType.Passive);

            if (target is null)
            {
                return;
            }

            var damage = _formulaService.CalculateDamage(playerState.Character, target.Data);
            target.Hp -= damage;
            target.Hp = Math.Max(target.Hp, 0);
            await playerState.CurrentMap.BroadcastPacket(new NpcReplyServerPacket
            {
                PlayerId = playerState.SessionId,
                PlayerDirection = playerState.Character.Direction,
                NpcIndex = playerState.CurrentMap.Npcs.ToList().IndexOf(target),
                Damage = damage,
                HpPercentage = (int)Math.Max((double)target.Hp / target.Data.Hp * 100, 0),
                KillStealProtection = NpcKillStealProtectionState.Unprotected,
            });

            if (target.Hp == 0)
            {
                playerState.Character.Exp += target.Data.Experience;
            }
        }

        await playerState.CurrentMap.BroadcastPacket(new AttackPlayerServerPacket
        {
            Direction = playerState.Character?.Direction ?? Direction.Down,
            PlayerId = playerState.SessionId
        }, except: playerState);

        _timeSinceLastAttack = DateTime.UtcNow;
    }

    public Task HandleAsync(PlayerState playerState, IPacket packet)
    {
        return HandleAsync(playerState, (AttackUseClientPacket)packet);
    }
}