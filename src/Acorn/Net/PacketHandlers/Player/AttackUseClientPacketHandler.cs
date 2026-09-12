using Acorn.Database.Repository;
using Acorn.Extensions;
using Acorn.Game.Services;
using Acorn.Options;
using Acorn.Shared.Caching;
using Acorn.World.Map;
using Acorn.World.Services.Arena;
using Acorn.World.Services.Combat;
using Acorn.World.Services.Map;
using Acorn.World.Services.Party;
using Acorn.World.Services.Player;
using Acorn.World.Services.Quest;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using Acorn.Infrastructure.Telemetry;
using Acorn.Net.PacketHandlers;
using NpcState = Acorn.World.Npc.NpcState;

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
    private readonly IMapTileService _tileService;
    private readonly int _rangedDistance;
    private readonly int _attackCooldownMs;
    private readonly bool _criticalFirstHit;
    private readonly IQuestService _questService;

    public AttackUseClientPacketHandler(UtcNowDelegate now, ILogger<AttackUseClientPacketHandler> logger,
        IFormulaService formulaService, IDataFileRepository dataFiles, ILootService lootService,
        IOptions<ServerOptions> serverOptions, ICharacterCacheService characterCache,
        IPaperdollService paperdollService, IArenaService arenaService, IPartyService partyService,
        IPlayerController playerController, IMapTileService tileService, IQuestService questService,
        AcornMetrics metrics)
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
        _tileService = tileService;
        _rangedDistance = serverOptions.Value.RangedDistance;
        _attackCooldownMs = serverOptions.Value.AttackCooldownMs;
        _criticalFirstHit = serverOptions.Value.CriticalFirstHit;
        _questService = questService;
        _metrics = metrics;
    }

    public async Task HandleAsync(PlayerState playerState, AttackUseClientPacket packet)
    {
        if (playerState.Character is null || playerState.CurrentMap is null)
        {
            return;
        }

        // Sitting players cannot attack (matches eoserv).
        if (playerState.Character.SitState != SitState.Stand)
        {
            return;
        }

        // Attack-rate limit (configurable, defaults to the previous 500ms).
        if ((_now() - playerState.LastAttackTime).TotalMilliseconds < _attackCooldownMs)
        {
            return;
        }

        var map = playerState.CurrentMap;
        var character = playerState.Character;

        // Use the direction from the packet (matches eoserv) and face that way.
        var direction = packet.Direction;
        character.Direction = direction;

        var range = GetAttackRange(character);
        var origin = character.AsCoords();

        var targetCoords = AttackTrace.FindTargetTile(
            origin,
            direction,
            range,
            coords => HasTargetAt(map, playerState.SessionId, coords),
            coords => IsBlocked(map, coords));

        // TEMP diagnostic: remove once ranged attacks are confirmed working.
        _logger.LogWarning(
            "RANGED DEBUG: char={Name} weaponId={WeaponId} eifItems={EifItems} subtype={Subtype} range={Range} dir={Dir} origin=({OX},{OY}) target={Target}",
            character.Name,
            character.Paperdoll.Weapon,
            _dataFiles.Eif.Items.Count,
            _dataFiles.Eif.GetItem(character.Paperdoll.Weapon)?.Subtype,
            range,
            direction,
            origin.X,
            origin.Y,
            targetCoords is null ? "none" : $"({targetCoords.X},{targetCoords.Y})");

        NpcState? target = null;
        PlayerState? targetPlayer = null;

        if (targetCoords is not null)
        {
            target = map.Npcs.Values.FirstOrDefault(x =>
                !x.IsDead &&
                x.X == targetCoords.X && x.Y == targetCoords.Y &&
                x.Data.Type is NpcType.Aggressive or NpcType.Passive);

            targetPlayer = target is null
                ? map.Players.Values.FirstOrDefault(p =>
                    p.SessionId != playerState.SessionId &&
                    p.Character is not null && !p.Character.Hidden &&
                    p.Character.X == targetCoords.X && p.Character.Y == targetCoords.Y)
                : null;
        }

        if (target is null)
        {
            if (targetPlayer is not null)
            {
                await HandlePlayerAttack(playerState, targetPlayer);
            }

            await BroadcastInRangeAsync(map, origin, new AttackPlayerServerPacket
            {
                Direction = direction,
                PlayerId = playerState.SessionId
            }, playerState);

            playerState.LastAttackTime = _now();
            return;
        }

        var remainingHp = target.Hp;
        var attackingBackOrSide = Math.Abs((int)target.Direction - (int)direction) != 2;

        var damage = _formulaService.CalculateDamageToNpc(character, target.Data, remainingHp,
            attackingBackOrSide: attackingBackOrSide, criticalFirstHit: _criticalFirstHit);

        // Report at most the damage needed to kill the target (matches eoserv's LimitDamage).
        damage = Math.Min(damage, remainingHp);
        target.Hp = Math.Max(target.Hp - damage, 0);

        // Register player as opponent for NPC aggro
        if (damage > 0)
        {
            target.AddOpponent(playerState.SessionId, damage);
        }

        var npcIndex = target.Index;
        var npcCoords = new Coords { X = target.X, Y = target.Y };
        var hpPercentage = (int)Math.Max((double)target.Hp / target.Data.Hp * 100, 0);

        await BroadcastInRangeAsync(map, npcCoords, new NpcReplyServerPacket
        {
            PlayerId = playerState.SessionId,
            PlayerDirection = direction,
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

            _metrics.NpcKills.Add(1,
                new("npc_id", target.Id),
                new("map_id", character.Map));

            _logger.NpcKilled(target.Data.Name, target.Id, character.Name!, character.Map);

            // Award experience from NPC data
            var experienceGained = target.Data.Experience;
            character.GainExperience(experienceGained);

            _metrics.ExperienceGained.Add(experienceGained);

            _logger.ExperienceGained(character.Name!, experienceGained, character.Exp, character.Level);

            // Check for level up(s)
            var levelsGained = 0;
            while (_formulaService.CanLevelUp(character))
            {
                var newLevel = _formulaService.LevelUp(character, _dataFiles.Ecf);
                levelsGained++;

                _metrics.LevelUps.Add(1);

                _logger.PlayerLeveledUp(character.Name!, newLevel);
            }

            // Cache character state if level up occurred
            if (levelsGained > 0)
            {
                await playerState.CacheCharacterStateAsync(_characterCache, _paperdollService);
            }

            // Roll for a drop (item or gold — gold is item ID 1, rolled from the NPC's
            // specific loot table plus the global drop table)
            var dropItem = _lootService.RollDrop(target.Id);
            var dropId = 0;
            var dropAmount = 0;
            var dropIndex = 0;

            if (dropItem != null)
            {
                dropAmount = _lootService.RollDropAmount(dropItem);
                dropId = dropItem.ItemId;

                // Create map item with killer's protection
                var itemIndex = map.GetNextItemIndex();
                var mapItem = new MapItem
                {
                    Id = dropId,
                    Amount = dropAmount,
                    Coords = new Coords { X = target.X, Y = target.Y },
                    OwnerId = playerState.SessionId,
                    ProtectedTicks = _dropProtectionTicks
                };

                map.Items.TryAdd(itemIndex, mapItem);
                dropIndex = itemIndex;

                // Gold is item ID 1; count NPC gold separately from item loot.
                if (dropId == 1)
                {
                    _metrics.NpcGoldDropped.Add(dropAmount);
                }
                else
                {
                    _metrics.NpcItemsDropped.Add(dropAmount);
                }

                _logger.LogInformation(
                    "Item drop spawned: ItemId={ItemId}, Amount={Amount}, Location=({X},{Y}), Owner={OwnerId}",
                    dropId, dropAmount, target.X, target.Y, playerState.SessionId);
            }

            var npcKilledData = new NpcKilledData
            {
                KillerId = playerState.SessionId,
                KillerDirection = direction,
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
                    Experience = character.Exp,
                    LevelUp = new LevelUpStats
                    {
                        Level = character.Level,
                        StatPoints = character.StatPoints,
                        SkillPoints = character.SkillPoints,
                        MaxHp = character.MaxHp,
                        MaxTp = character.MaxTp,
                        MaxSp = character.MaxSp
                    }
                });
            }
            else
            {
                // Send NpcSpecServerPacket with experience to the killer
                await playerState.Send(new NpcSpecServerPacket
                {
                    NpcKilledData = npcKilledData,
                    Experience = character.Exp
                });
            }

            // Broadcast death to others in range (without experience)
            await BroadcastInRangeAsync(map, npcCoords, new NpcSpecServerPacket
            {
                NpcKilledData = npcKilledData
            }, playerState);

            // Advance any NPC-kill quest objectives for the killer
            await _questService.NotifyNpcKilled(playerState, target.Id);
        }

        await BroadcastInRangeAsync(map, origin, new AttackPlayerServerPacket
        {
            Direction = direction,
            PlayerId = playerState.SessionId
        }, playerState);

        playerState.LastAttackTime = _now();
    }

    /// <summary>
    ///     Gets the maximum attack distance: <see cref="ServerOptions.RangedDistance" /> when a
    ///     ranged weapon is equipped, otherwise 1 (melee).
    /// </summary>
    private int GetAttackRange(Acorn.Game.Models.Character character)
    {
        var weapon = _dataFiles.Eif.GetItem(character.Paperdoll.Weapon);
        return AttackTrace.GetRange(weapon?.Subtype, _rangedDistance);
    }

    private static bool HasTargetAt(MapState map, int attackerSessionId, Coords coords)
    {
        if (map.Npcs.Values.Any(n =>
                !n.IsDead && n.X == coords.X && n.Y == coords.Y &&
                n.Data.Type is NpcType.Aggressive or NpcType.Passive))
        {
            return true;
        }

        // Players are only valid targets on PvP-enabled maps.
        if (!IsPvpEnabled(map))
        {
            return false;
        }

        return map.Players.Values.Any(p =>
            p.SessionId != attackerSessionId &&
            p.Character is not null && !p.Character.Hidden &&
            p.Character.X == coords.X && p.Character.Y == coords.Y);
    }

    private static bool IsPvpEnabled(MapState map)
    {
        return map.IsArenaMap || map.Data.Type == Moffat.EndlessOnline.SDK.Protocol.Map.MapType.Pk;
    }

    private bool IsBlocked(MapState map, Coords coords)
    {
        if (coords.X < 0 || coords.Y < 0 || coords.X >= map.Data.Width || coords.Y >= map.Data.Height)
        {
            return true;
        }

        return !_tileService.IsTileWalkable(map.Data, coords);
    }

    /// <summary>
    ///     Sends <paramref name="packet" /> to every player within client render range of
    ///     <paramref name="origin" /> instead of the whole map.
    /// </summary>
    private async Task BroadcastInRangeAsync(MapState map, Coords origin, IPacket packet, PlayerState? except = null)
    {
        var recipients = map.Players.Values
            .Where(p => p.Character is not null)
            .Where(p => except is null || p.SessionId != except.SessionId)
            .Where(p => _tileService.InClientRange(origin, p.Character!.AsCoords()))
            .Select(p => p.Send(packet));

        await Task.WhenAll(recipients);
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

        var remainingHp = target.Character.Hp;
        var damage = _formulaService.CalculateDamageToPlayer(attacker.Character, target.Character,
            attackingBackOrSide, criticalFirstHit: _criticalFirstHit);

        // Report at most the damage needed to kill the target (matches eoserv's LimitDamage).
        damage = Math.Min(damage, remainingHp);
        target.Character.Hp = Math.Max(0, target.Character.Hp - damage);

        var dead = target.Character.Hp == 0;
        var hpPercentage = (int)Math.Round(target.Character.Hp * 100.0 / target.Character.MaxHp);

        await BroadcastInRangeAsync(map, target.Character.AsCoords(), new AvatarReplyServerPacket
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
            _metrics.PvPKills.Add(1);
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