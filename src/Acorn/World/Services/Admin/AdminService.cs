using Acorn.Database.Models;
using Acorn.Database.Repository;
using Acorn.Net;
using Acorn.Net.PacketHandlers.Board;
using Acorn.Net.Services;
using Acorn.Options;
using Acorn.World.Services.Bans;
using Acorn.World.Services.Player;
using Microsoft.Extensions.DependencyInjection;
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
    IBanService banService,
    IOptions<ServerOptions> serverOptions,
    IServiceScopeFactory scopeFactory,
    ILogger<AdminService> logger) : IAdminService
{
    // Minimum admin level that receives help requests and player reports.
    // Matches the threshold used for admin chat (TalkAdminClientPacketHandler).
    private const AdminLevel MinRequestRecipientLevel = AdminLevel.Guardian;

    // Jail location - falls back to the rescue/spawn location when not explicitly configured
    private int JailMap => serverOptions.Value.Jail?.Map ?? serverOptions.Value.Rescue?.Map ?? serverOptions.Value.NewCharacter.Map;
    private int JailX => serverOptions.Value.Jail?.X ?? serverOptions.Value.Rescue?.X ?? serverOptions.Value.NewCharacter.X;
    private int JailY => serverOptions.Value.Jail?.Y ?? serverOptions.Value.Rescue?.Y ?? serverOptions.Value.NewCharacter.Y;

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

        if (target.Account?.Username is { } username)
        {
            banService.Ban(BanKeys.Username(username), reason: $"banned by {admin.Character.Name}");
        }

        if (!string.IsNullOrWhiteSpace(target.Hdid))
        {
            banService.Ban(BanKeys.Hdid(target.Hdid), reason: $"banned by {admin.Character.Name}");
        }

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

        target.MutedUntil = DateTime.UtcNow.AddSeconds(serverOptions.Value.MuteLengthSeconds);
        logger.LogInformation("Admin {Admin} muted player {Target} for {Seconds}s", admin.Character!.Name, targetName,
            serverOptions.Value.MuteLengthSeconds);

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

        target.MutedUntil = DateTime.MinValue;
        logger.LogInformation("Admin {Admin} unmuted player {Target}", admin.Character!.Name, targetName);
        await notifications.SystemMessage(target, "You have been unmuted.");
        await notifications.SystemMessage(admin, $"Player '{targetName}' has been unmuted.");
    }

    public async Task SendHelpRequestAsync(PlayerState sender, string message)
    {
        if (sender.Character is null)
        {
            return;
        }

        var playerName = sender.Character.Name ?? string.Empty;
        logger.LogInformation("Player {Character} requested admin help: {Message}", playerName, message);

        // Any player may ask for help; deliver the request to online admins as an
        // AdminInteract/Reply (Message) packet so the client shows the help popup.
        await BroadcastToAdmins(new AdminInteractReplyServerPacket
        {
            MessageType = AdminMessageType.Message,
            MessageTypeData = new AdminInteractReplyServerPacket.MessageTypeDataMessage
            {
                PlayerName = playerName,
                Message = message
            }
        });

        await notifications.SystemMessage(sender, "Your help request has been sent to the online admins.");
    }

    public async Task SendReportAsync(PlayerState sender, string reportee, string message)
    {
        if (sender.Character is null)
        {
            return;
        }

        var reporter = sender.Character.Name ?? string.Empty;
        logger.LogInformation("Player {Character} reported {Reportee}: {Message}", reporter, reportee, message);

        // Deliver the report to online admins as an AdminInteract/Reply (Report) packet.
        await BroadcastToAdmins(new AdminInteractReplyServerPacket
        {
            MessageType = AdminMessageType.Report,
            MessageTypeData = new AdminInteractReplyServerPacket.MessageTypeDataReport
            {
                PlayerName = reporter,
                Message = message,
                ReporteeName = reportee
            }
        });

        await PersistReportAsync(reporter, reportee, message);

        await notifications.SystemMessage(sender, $"Your report about {reportee} has been submitted.");
    }

    private async Task BroadcastToAdmins(IPacket packet)
    {
        var sends = world.GetAllPlayers()
            .Where(p => p.Character is not null && p.Character.Admin >= MinRequestRecipientLevel)
            .Select(p => p.Send(packet));

        await Task.WhenAll(sends);
    }

    private async Task PersistReportAsync(string reporter, string reportee, string message)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var boardRepository = scope.ServiceProvider.GetRequiredService<IBoardRepository>();

            await boardRepository.CreatePostAsync(new BoardPost
            {
                BoardId = BoardRules.AdminBoardId,
                CharacterName = Truncate(reporter, 16),
                Subject = Truncate($"[Report] {reporter} reports: {reportee}", 64),
                Body = Truncate(message, 2048)
            });
        }
        catch (Exception e)
        {
            // Persisting is best effort - never let a DB failure break the live notification.
            logger.LogError(e, "Failed to persist report from {Reporter} about {Reportee}", reporter, reportee);
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
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

        if (admin.CurrentMap is not null)
        {
            if (admin.Character.Hidden)
            {
                // AdminInteract/Remove tells nearby clients this player has disappeared.
                await admin.CurrentMap.BroadcastPacket(new AdminInteractRemoveServerPacket
                {
                    PlayerId = admin.SessionId
                });

                await notifications.SystemMessage(admin, "You are now hidden.");
            }
            else
            {
                // AdminInteract/Agree tells nearby clients this player has appeared.
                await admin.CurrentMap.BroadcastPacket(new AdminInteractAgreeServerPacket
                {
                    PlayerId = admin.SessionId
                });

                await notifications.SystemMessage(admin, "You are now visible.");
            }
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
