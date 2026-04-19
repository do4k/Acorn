using Acorn.Net;
using Acorn.Net.Services;
using Acorn.Options;
using Acorn.World.Services.Player;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.World.Services.Admin;

public class AdminService(
    IWorldQueries world,
    IPlayerController playerController,
    INotificationService notifications,
    IOptions<ServerOptions> serverOptions,
    ILogger<AdminService> logger) : IAdminService
{
    // Jail location - defaults to rescue/spawn location
    private int JailMap => serverOptions.Value.Rescue?.Map ?? serverOptions.Value.NewCharacter.Map;
    private int JailX => serverOptions.Value.Rescue?.X ?? serverOptions.Value.NewCharacter.X;
    private int JailY => serverOptions.Value.Rescue?.Y ?? serverOptions.Value.NewCharacter.Y;

    public async Task KickPlayerAsync(PlayerState admin, string targetName)
    {
        if (!RequireAdminLevel(admin, AdminLevel.Guardian))
            return;

        var target = world.FindPlayerByName(targetName);
        if (target is null)
        {
            await notifications.SystemMessage(admin, $"Player '{targetName}' is not online.");
            return;
        }

        logger.LogInformation("Admin {Admin} kicked player {Target}", admin.Character!.Name, targetName);

        await BroadcastServerMessage($"{targetName} has been kicked by {admin.Character!.Name}.");
        target.Disconnect();
    }

    public async Task BanPlayerAsync(PlayerState admin, string targetName)
    {
        if (!RequireAdminLevel(admin, AdminLevel.GameMaster))
            return;

        var target = world.FindPlayerByName(targetName);
        if (target is null)
        {
            await notifications.SystemMessage(admin, $"Player '{targetName}' is not online.");
            return;
        }

        logger.LogInformation("Admin {Admin} banned player {Target}", admin.Character!.Name, targetName);

        await BroadcastServerMessage($"{targetName} has been banned by {admin.Character!.Name}.");
        target.Disconnect();
    }

    public async Task JailPlayerAsync(PlayerState admin, string targetName)
    {
        if (!RequireAdminLevel(admin, AdminLevel.GameMaster))
            return;

        var target = world.FindPlayerByName(targetName);
        if (target is null)
        {
            await notifications.SystemMessage(admin, $"Player '{targetName}' is not online.");
            return;
        }

        var jailMap = world.FindMap(JailMap);
        if (jailMap is null)
        {
            await notifications.SystemMessage(admin, "Jail map not configured.");
            return;
        }

        target.IsJailed = true;
        logger.LogInformation("Admin {Admin} jailed player {Target}", admin.Character!.Name, targetName);

        await playerController.WarpAsync(target, jailMap, JailX, JailY, WarpEffect.Admin);
        await BroadcastServerMessage($"{targetName} has been jailed by {admin.Character!.Name}.");
    }

    public async Task FreePlayerAsync(PlayerState admin, string targetName)
    {
        if (!RequireAdminLevel(admin, AdminLevel.GameMaster))
            return;

        var target = world.FindPlayerByName(targetName);
        if (target is null)
        {
            await notifications.SystemMessage(admin, $"Player '{targetName}' is not online.");
            return;
        }

        target.IsJailed = false;

        // Warp to new character spawn (home)
        var homeMap = world.FindMap(serverOptions.Value.NewCharacter.Map);
        if (homeMap is not null)
        {
            await playerController.WarpAsync(target, homeMap,
                serverOptions.Value.NewCharacter.X,
                serverOptions.Value.NewCharacter.Y,
                WarpEffect.Admin);
        }

        logger.LogInformation("Admin {Admin} freed player {Target}", admin.Character!.Name, targetName);
        await BroadcastServerMessage($"{targetName} has been freed by {admin.Character!.Name}.");
    }

    public async Task FreezePlayerAsync(PlayerState admin, string targetName)
    {
        if (!RequireAdminLevel(admin, AdminLevel.Guardian))
            return;

        var target = world.FindPlayerByName(targetName);
        if (target is null)
        {
            await notifications.SystemMessage(admin, $"Player '{targetName}' is not online.");
            return;
        }

        target.IsFrozen = true;
        logger.LogInformation("Admin {Admin} froze player {Target}", admin.Character!.Name, targetName);

        // Send WalkCloseServerPacket to freeze client-side movement
        await target.Send(new WalkCloseServerPacket());
        await BroadcastServerMessage($"{targetName} has been frozen by {admin.Character!.Name}.");
    }

    public async Task UnfreezePlayerAsync(PlayerState admin, string targetName)
    {
        if (!RequireAdminLevel(admin, AdminLevel.Guardian))
            return;

        var target = world.FindPlayerByName(targetName);
        if (target is null)
        {
            await notifications.SystemMessage(admin, $"Player '{targetName}' is not online.");
            return;
        }

        target.IsFrozen = false;
        logger.LogInformation("Admin {Admin} unfroze player {Target}", admin.Character!.Name, targetName);
        await notifications.SystemMessage(target, "You have been unfrozen.");
        await notifications.SystemMessage(admin, $"Player '{targetName}' has been unfrozen.");
    }

    public async Task MutePlayerAsync(PlayerState admin, string targetName)
    {
        if (!RequireAdminLevel(admin, AdminLevel.GameMaster))
            return;

        var target = world.FindPlayerByName(targetName);
        if (target is null)
        {
            await notifications.SystemMessage(admin, $"Player '{targetName}' is not online.");
            return;
        }

        target.IsMuted = true;
        logger.LogInformation("Admin {Admin} muted player {Target}", admin.Character!.Name, targetName);

        // Send TalkSpecServerPacket to notify client
        await target.Send(new TalkSpecServerPacket { AdminName = admin.Character!.Name! });
        await BroadcastServerMessage($"{targetName} has been muted by {admin.Character!.Name}.");
    }

    public async Task UnmutePlayerAsync(PlayerState admin, string targetName)
    {
        if (!RequireAdminLevel(admin, AdminLevel.GameMaster))
            return;

        var target = world.FindPlayerByName(targetName);
        if (target is null)
        {
            await notifications.SystemMessage(admin, $"Player '{targetName}' is not online.");
            return;
        }

        target.IsMuted = false;
        logger.LogInformation("Admin {Admin} unmuted player {Target}", admin.Character!.Name, targetName);
        await notifications.SystemMessage(target, "You have been unmuted.");
        await notifications.SystemMessage(admin, $"Player '{targetName}' has been unmuted.");
    }

    public async Task GetPlayerInfoAsync(PlayerState admin, string targetName)
    {
        if (!RequireAdminLevel(admin, AdminLevel.LightGuide))
            return;

        var target = world.FindPlayerByName(targetName);
        if (target?.Character is null)
        {
            await notifications.SystemMessage(admin, $"Player '{targetName}' is not online.");
            return;
        }

        var c = target.Character;
        await admin.Send(new AdminInteractTellServerPacket
        {
            Name = c.Name ?? targetName,
            Usage = c.Usage,
            GoldBank = c.GoldBank,
            Exp = c.Exp,
            Level = c.Level,
            MapId = c.Map,
            MapCoords = new BigCoords { X = c.X, Y = c.Y },
            Stats = new CharacterStatsInfoLookup
            {
                Hp = c.Hp,
                MaxHp = c.MaxHp,
                Tp = c.Tp,
                MaxTp = c.MaxTp,
                BaseStats = new CharacterBaseStats
                {
                    Str = c.AdjStr,
                    Intl = c.AdjInt,
                    Wis = c.AdjWis,
                    Agi = c.AdjAgi,
                    Con = c.AdjCon,
                    Cha = c.AdjCha
                },
                SecondaryStats = new CharacterSecondaryStatsInfoLookup
                {
                    MinDamage = c.MinDamage,
                    MaxDamage = c.MaxDamage,
                    Accuracy = c.Accuracy,
                    Evade = c.Evade,
                    Armor = c.Armor
                },
                ElementalStats = new CharacterElementalStats()
            },
            Weight = new Weight
            {
                Current = 0,
                Max = c.MaxWeight
            }
        });
    }

    public async Task GetPlayerInventoryAsync(PlayerState admin, string targetName)
    {
        if (!RequireAdminLevel(admin, AdminLevel.GameMaster))
            return;

        var target = world.FindPlayerByName(targetName);
        if (target?.Character is null)
        {
            await notifications.SystemMessage(admin, $"Player '{targetName}' is not online.");
            return;
        }

        var c = target.Character;
        await admin.Send(new AdminInteractListServerPacket
        {
            Name = c.Name ?? targetName,
            Usage = c.Usage,
            GoldBank = c.GoldBank,
            Inventory = c.Inventory.Items.Select(i => new Item { Id = i.Id, Amount = i.Amount }).ToList(),
            Bank = c.Bank.Items.Select(i => new ThreeItem { Id = i.Id, Amount = i.Amount }).ToList()
        });
    }

    public async Task TriggerQuakeAsync(PlayerState admin, int strength)
    {
        if (!RequireAdminLevel(admin, AdminLevel.GameMaster))
            return;

        logger.LogInformation("Admin {Admin} triggered quake with strength {Strength}",
            admin.Character!.Name, strength);

        var quakePacket = new EffectUseServerPacket
        {
            Effect = MapEffect.Quake,
            EffectData = new EffectUseServerPacket.EffectDataQuake
            {
                QuakeStrength = Math.Clamp(strength, 1, 8)
            }
        };

        foreach (var map in world.GetAllMaps())
        {
            await map.BroadcastPacket(quakePacket);
        }
    }

    public async Task EvacuateMapAsync(PlayerState admin)
    {
        if (!RequireAdminLevel(admin, AdminLevel.GameMaster))
            return;

        if (admin.CurrentMap is null)
            return;

        var homeMap = world.FindMap(serverOptions.Value.NewCharacter.Map);
        if (homeMap is null)
            return;

        logger.LogInformation("Admin {Admin} evacuated map {MapId}",
            admin.Character!.Name, admin.CurrentMap.Id);

        var players = admin.CurrentMap.Players.Values
            .Where(p => p.SessionId != admin.SessionId && p.Character is not null)
            .ToList();

        foreach (var player in players)
        {
            await playerController.WarpAsync(player, homeMap,
                serverOptions.Value.NewCharacter.X,
                serverOptions.Value.NewCharacter.Y,
                WarpEffect.Admin);
        }

        await notifications.SystemMessage(admin, $"Evacuated {players.Count} players from map {admin.CurrentMap.Id}.");
    }

    public async Task ToggleHideAsync(PlayerState admin)
    {
        if (!RequireAdminLevel(admin, AdminLevel.Guardian))
            return;

        if (admin.Character is null)
            return;

        admin.Character.Hidden = !admin.Character.Hidden;
        logger.LogInformation("Admin {Admin} is now {State}",
            admin.Character.Name, admin.Character.Hidden ? "hidden" : "visible");

        if (admin.Character.Hidden)
        {
            // Remove from other players' view
            if (admin.CurrentMap is not null)
            {
                await admin.CurrentMap.NotifyLeave(admin);
                // Re-add to map players list so they can still see others
                admin.CurrentMap.Players.TryAdd(admin.SessionId, admin);
                admin.CurrentMap = admin.CurrentMap;
            }

            await notifications.SystemMessage(admin, "You are now hidden.");
        }
        else
        {
            // Show to other players
            if (admin.CurrentMap is not null)
            {
                await admin.CurrentMap.BroadcastPacket(new PlayersAgreeServerPacket
                {
                    Nearby = admin.CurrentMap.AsNearbyInfo(null, WarpEffect.Admin)
                }, admin);
            }

            await notifications.SystemMessage(admin, "You are now visible.");
        }
    }

    public async Task GlobalMessageAsync(PlayerState admin, string message)
    {
        if (!RequireAdminLevel(admin, AdminLevel.GameMaster))
            return;

        logger.LogInformation("Admin {Admin} sent global message: {Message}",
            admin.Character!.Name, message);

        var packet = new TalkServerServerPacket
        {
            Message = $"[Server] {message}"
        };

        foreach (var player in world.GetAllPlayers())
        {
            await player.Send(packet);
        }
    }

    private bool RequireAdminLevel(PlayerState admin, AdminLevel requiredLevel)
    {
        return admin.Character is not null && admin.Character.Admin >= requiredLevel;
    }

    private async Task BroadcastServerMessage(string message)
    {
        var packet = new TalkServerServerPacket { Message = message };
        foreach (var player in world.GetAllPlayers())
        {
            await player.Send(packet);
        }
    }
}
