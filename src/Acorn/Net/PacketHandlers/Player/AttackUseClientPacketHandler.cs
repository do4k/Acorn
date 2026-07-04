using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Game.Services;
using Acorn.Options;
using Acorn.Shared.Caching;
using Acorn.World.Map;
using Acorn.World.Services.Arena;
using Acorn.World.Services.Party;
using Acorn.World.Services.Player;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using Acorn.Infrastructure.Telemetry;
using Acorn.Net.PacketHandlers;

namespace Acorn.Net.PacketHandlers.Player;

[RequiresCharacter]
internal class AttackUseClientPacketHandler : IPacketHandler<AttackUseClientPacket>
{
    private readonly IDataFileRepository _dataFiles;
    private readonly int _dropProtectionTicks;
    private readonly IFormulaService _formulaService;
    private readonly ILogger<AttackUseClientPacketHandler> _logger;
    private readonly AcornMetrics _metrics;
    private readonly ILootService _lootService;
    private readonly UtcNowDelegate _now;
    private readonly ICharacterCacheService _characterCache;
    private readonly IPaperdollService _paperdollService;
    private readonly IArenaService _arenaService;
    private readonly IPartyService _partyService;
    private readonly IPlayerController _playerController;
    private DateTime _timeSinceLastAttack;

    public AttackUseClientPacketHandler(UtcNowDelegate now, ILogger<AttackUseClientPacketHandler> logger,
        IFormulaService formulaService, IDataFileRepository dataFiles, ILootService lootService,
        IOptions<ServerOptions> serverOptions, ICharacterCacheService characterCache,
        IPaperdollService paperdollService, IArenaService arenaService, IPartyService partyService,
        IPlayerController playerController, AcornMetrics metrics)
    {
        _now = now;
        _logger = logger;
        _formulaService = formulaService;
        _dataFiles = dataFiles;
        _lootService = lootService;
        _dropProtectionTicks = serverOptions.Value.DropProtectionTicks;
        _characterCache = characterCache;
        _paperdollService = paperdollService;
        _arenaService = arenaService;
        _partyService = partyService;
        _playerController = playerController;
        _metrics = metrics;
    }

    public async Task HandleAsync(PlayerState playerState, AttackUseClientPacket packet)
    {
        if ((_now() - _timeSinceLastAttack).TotalMilliseconds < 500)
        {
            return;
        }

        if (playerState.Character is not null)
        {
            var nextCoords = playerState.Character!.NextCoords();
            var target = playerState.CurrentMap!.Npcs.Values.FirstOrDefault(x =>
                !x.IsDead &&
                x.X == nextCoords.X && x.Y == nextCoords.Y &&
                x.Data.Type is NpcType.Aggressive or NpcType.Passive);

            if (target is null)
            {
                var targetPlayer = playerState.CurrentMap.Players.Values.FirstOrDefault(p =>
                    p.SessionId != playerState.SessionId &&
                    p.Character != null && !p.Character.Hidden &&
                    p.Character.X == nextCoords.X && p.Character.Y == nextCoords.Y);

                if (targetPlayer is not null)
                {
                    await HandlePlayerAttack(playerState, targetPlayer);
                }

                await playerState.CurrentMap!.BroadcastPacket(new AttackPlayerServerPacket
                {
                    Direction = playerState.Character?.Direction ?? Direction.Down,
                    PlayerId = playerState.SessionId
                }, playerState);

                _timeSinceLastAttack = DateTime.UtcNow;
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

            var npcIndex = playerState.CurrentMap.Npcs.Values.ToList().IndexOf(target);
            var hpPercentage = (int)Math.Max((double)target.Hp / target.Data.Hp * 100, 0);

            await playerState.CurrentMap.BroadcastPacket(new NpcReplyServerPacket
            {
                PlayerId = playerState.SessionId,
                PlayerDirection = playerState.Character.Direction,
                NpcIndex = npcIndex,
                Damage = damage,
                HpPercentage = hpPercentage,
                KillStealProtection = NpcKillStealProtectionState.Unprotected
            });

            // Handle NPC death
            if (target.Hp == 0 && !target.IsDead)
            {
                target.IsDead = true;
                target.DeathTime = DateTime.UtcNow;
                target.Opponents.Clear();

                _metrics.NpcKills.Add(1);

                _logger.NpcKilled(target.Data.Name, target.Id, playerState.Character.Name!, playerState.Character.Map);

                // Award experience from NPC data
                var experienceGained = target.Data.Experience;
                playerState.Character.GainExperience(experienceGained);

                _metrics.ExperienceGained.Add(experienceGained);

                _logger.ExperienceGained(playerState.Character.Name!, experienceGained, playerState.Character.Exp, playerState.Character.Level);

                // Check for level up(s)
                var levelsGained = 0;
                while (_formulaService.CanLevelUp(playerState.Character))
                {
                    var newLevel = _formulaService.LevelUp(playerState.Character, _dataFiles.Ecf);
                    levelsGained++;

                    _metrics.LevelUps.Add(1);

                    _logger.PlayerLeveledUp(playerState.Character.Name!, newLevel);
                }

                // Cache character state if level up occurred
                if (levelsGained > 0)
                {
                    await playerState.CacheCharacterStateAsync(_characterCache, _paperdollService);
                }

                // Roll for item drop
                var dropItem = _lootService.RollDrop(target.Id);
                var dropId = 0;
                var dropAmount = 0;
                var dropIndex = 0;

                if (dropItem != null)
                {
                    dropAmount = _lootService.RollDropAmount(dropItem);
                    dropId = dropItem.ItemId;

                    // Create map item with killer's protection
                    var itemIndex = playerState.CurrentMap.GetNextItemIndex();
                    var mapItem = new MapItem
                    {
                        Id = dropId,
                        Amount = dropAmount,
                        Coords = new Coords { X = target.X, Y = target.Y },
                        OwnerId = playerState.SessionId,
                        ProtectedTicks = _dropProtectionTicks
                    };

                    playerState.CurrentMap.Items.TryAdd(itemIndex, mapItem);
                    dropIndex = itemIndex;

                    _logger.LogInformation(
                        "Item drop spawned: ItemId={ItemId}, Amount={Amount}, Location=({X},{Y}), Owner={OwnerId}",
                        dropId, dropAmount, target.X, target.Y, playerState.SessionId);
                }

                var npcKilledData = new NpcKilledData
                {
                    KillerId = playerState.SessionId,
                    KillerDirection = playerState.Character.Direction,
                    NpcIndex = npcIndex,
                    DropIndex = dropIndex,
                    DropId = dropId,
                    DropAmount = dropAmount,
                    DropCoords = new Coords { X = target.X, Y = target.Y },
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
                    }, playerState);
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
                    }, playerState);
                }

                // TODO: Handle NPC drops (items, gold)
            }
        }

        await playerState.CurrentMap!.BroadcastPacket(new AttackPlayerServerPacket
        {
            Direction = playerState.Character?.Direction ?? Direction.Down,
            PlayerId = playerState.SessionId
        }, playerState);

        _timeSinceLastAttack = DateTime.UtcNow;
    }

    private async Task HandlePlayerAttack(PlayerState attacker, PlayerState target)
    {
        if (attacker.Character is null || target.Character is null || attacker.CurrentMap is null)
        {
            return;
        }

        var map = attacker.CurrentMap;

        var isArenaMatch = map.IsArenaMap &&
                            map.ArenaPlayers.Any(p => p.SessionId == attacker.SessionId && !p.IsDead) &&
                            map.ArenaPlayers.Any(p => p.SessionId == target.SessionId && !p.IsDead);

        if (isArenaMatch)
        {
            await _arenaService.HandleArenaAttackAsync(attacker, target);
            return;
        }

        if (map.Data.Type != Moffat.EndlessOnline.SDK.Protocol.Map.MapType.Pk)
        {
            return;
        }

        var party = _partyService.GetPlayerParty(attacker.SessionId);
        if (party is not null && party.Members.Contains(target.SessionId))
        {
            return;
        }

        var attackingBackOrSide =
            Math.Abs((int)target.Character.Direction - (int)attacker.Character.Direction) != 2;

        var damage = _formulaService.CalculateDamageToPlayer(attacker.Character, target.Character, attackingBackOrSide);
        target.Character.Hp = Math.Max(0, target.Character.Hp - damage);

        var dead = target.Character.Hp == 0;
        var hpPercentage = (int)Math.Round(target.Character.Hp * 100.0 / target.Character.MaxHp);

        await map.BroadcastPacket(new AvatarReplyServerPacket
        {
            PlayerId = attacker.SessionId,
            VictimId = target.SessionId,
            Damage = damage,
            Direction = attacker.Character.Direction,
            HpPercentage = hpPercentage,
            Dead = dead
        });

        _logger.LogInformation("Player {Attacker} dealt {Damage} PvP damage to {Target} on map {MapId}",
            attacker.Character.Name, damage, target.Character.Name, map.Id);

        if (dead)
        {
            await _playerController.DieAsync(target);
        }

        await target.Send(new RecoverPlayerServerPacket
        {
            Hp = target.Character.Hp,
            Tp = target.Character.Tp
        });

        await _partyService.BroadcastHpUpdate(target);
    }

}