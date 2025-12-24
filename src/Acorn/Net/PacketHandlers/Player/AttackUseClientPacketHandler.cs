using Acorn.Database.Repository;
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
    private readonly IFormulaService _formulaService;
    private readonly IDataFileRepository _dataFiles;

    public AttackUseClientPacketHandler(UtcNowDelegate now, ILogger<AttackUseClientPacketHandler> logger, IFormulaService formulaService, IDataFileRepository dataFiles)
    {
        _now = now;
        _logger = logger;
        _formulaService = formulaService;
        _dataFiles = dataFiles;
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
                !x.IsDead &&
                x.X == nextCoords.X && x.Y == nextCoords.Y &&
                x.Data.Type is NpcType.Aggressive or NpcType.Passive);

            if (target is null)
            {
                return;
            }

            var damage = _formulaService.CalculateDamageToNpc(playerState.Character, target.Data);
            target.Hp -= damage;
            target.Hp = Math.Max(target.Hp, 0);

            // Register player as opponent for NPC aggro
            if (damage > 0)
            {
                target.AddOpponent(playerState.SessionId, damage);
            }

            var npcIndex = playerState.CurrentMap.Npcs.ToList().IndexOf(target);
            var hpPercentage = (int)Math.Max((double)target.Hp / target.Data.Hp * 100, 0);

            await playerState.CurrentMap.BroadcastPacket(new NpcReplyServerPacket
            {
                PlayerId = playerState.SessionId,
                PlayerDirection = playerState.Character.Direction,
                NpcIndex = npcIndex,
                Damage = damage,
                HpPercentage = hpPercentage,
                KillStealProtection = NpcKillStealProtectionState.Unprotected,
            });

            // Handle NPC death
            if (target.Hp == 0 && !target.IsDead)
            {
                target.IsDead = true;
                target.DeathTime = DateTime.UtcNow;
                target.Opponents.Clear();

                _logger.LogInformation("NPC {NpcName} (ID: {NpcId}) killed by {PlayerName}",
                    target.Data.Name, target.Id, playerState.Character.Name);

                // Award experience from NPC data
                var experienceGained = target.Data.Experience;
                playerState.Character.GainExperience(experienceGained);

                _logger.LogInformation("Player {PlayerName} gained {Exp} experience (Level {Level}, Total Exp: {TotalExp})",
                    playerState.Character.Name, experienceGained, playerState.Character.Level, playerState.Character.Exp);

                // Check for level up(s)
                int levelsGained = 0;
                while (_formulaService.CanLevelUp(playerState.Character))
                {
                    var newLevel = _formulaService.LevelUp(playerState.Character, _dataFiles.Ecf);
                    levelsGained++;

                    _logger.LogInformation("Player {PlayerName} leveled up to level {Level}!",
                        playerState.Character.Name, newLevel);
                }

                var npcKilledData = new NpcKilledData
                {
                    KillerId = playerState.SessionId,
                    KillerDirection = playerState.Character.Direction,
                    NpcIndex = npcIndex,
                    DropIndex = 0, // TODO: Handle item drops
                    DropId = 0,
                    DropAmount = 0,
                    Damage = damage
                };

                if (levelsGained > 0)
                {
                    // Send NpcAcceptServerPacket for level up (includes experience and level up stats)
                    await playerState.Send(new NpcAcceptServerPacket
                    {
                        NpcKilledData = npcKilledData,
                        Experience = playerState.Character.Exp,
                        LevelUp = new LevelUpStats
                        {
                            Level = playerState.Character.Level,
                            StatPoints = playerState.Character.StatPoints,
                            SkillPoints = playerState.Character.SkillPoints,
                            MaxHp = playerState.Character.MaxHp,
                            MaxTp = playerState.Character.MaxTp,
                            MaxSp = playerState.Character.MaxSp
                        }
                    });

                    // Broadcast death to others (without experience)
                    await playerState.CurrentMap.BroadcastPacket(new NpcSpecServerPacket
                    {
                        NpcKilledData = npcKilledData
                    }, except: playerState);
                }
                else
                {
                    // Send NpcSpecServerPacket with experience to the killer
                    await playerState.Send(new NpcSpecServerPacket
                    {
                        NpcKilledData = npcKilledData,
                        Experience = playerState.Character.Exp
                    });

                    // Broadcast death to others (without experience)
                    await playerState.CurrentMap.BroadcastPacket(new NpcSpecServerPacket
                    {
                        NpcKilledData = npcKilledData
                    }, except: playerState);
                }

                // TODO: Handle NPC drops (items, gold)
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