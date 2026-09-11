using System.Collections.Concurrent;
using Acorn.Database;
using Acorn.Extensions;
using Acorn.Game.Services;
using Acorn.Net;
using Acorn.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.World.Services.Guild;

public class GuildService(
    IServiceScopeFactory scopeFactory,
    IWorldQueries world,
    IInventoryService inventoryService,
    IOptions<GuildOptions> options,
    ILogger<GuildService> logger) : IGuildService
{
    private readonly GuildOptions _options = options.Value;

    // Track guild creation sessions per leader session id
    private readonly ConcurrentDictionary<int, GuildCreation> _creations = new();

    public async Task OpenGuildMaster(PlayerState player, int npcIndex)
    {
        if (player.Character is null || player.CurrentMap is null) return;

        if (!player.CurrentMap.Npcs.TryGetValue(npcIndex, out var npc)) return;
        if (npc.Data.Type != NpcType.Guild) return;

        player.InteractingNpcIndex = npcIndex;

        await player.Send(new GuildOpenServerPacket
        {
            SessionId = player.SessionId
        });
    }

    public async Task CreateGuildRequest(PlayerState player, int sessionId, string guildTag, string guildName)
    {
        if (player.Character is null || player.CurrentMap is null) return;
        if (player.SessionId != sessionId) return;
        if (player.InteractingNpcIndex is null) return;

        guildTag = guildTag.Trim().ToUpperInvariant();
        guildName = guildName.Trim().ToLowerInvariant();

        if (!GuildRules.IsValidTag(guildTag, _options) || !GuildRules.IsValidName(guildName, _options))
        {
            await SendGuildReply(player, GuildReply.NotApproved);
            return;
        }

        if (player.Character.GuildTag is not null)
        {
            return;
        }

        if (inventoryService.GetItemAmount(player.Character, GuildRules.GoldItemId) < _options.Price)
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AcornDbContext>();

        if (await GuildExists(db, guildTag, guildName))
        {
            await SendGuildReply(player, GuildReply.Exists);
            return;
        }

        // Nearby unguilded players that can be invited as founding members.
        var candidates = player.CurrentMap.Players.Values
            .Where(p => p.SessionId != player.SessionId && p.Character?.GuildTag is null)
            .ToList();

        if (!GuildRules.HasEnoughCandidates(candidates.Count, _options))
        {
            await SendGuildReply(player, GuildReply.NoCandidates);
            return;
        }

        _creations[player.SessionId] = new GuildCreation(guildTag, guildName);

        await player.Send(GuildPackets.CreateBegin());

        // eoserv always invites nearby unguilded players, even when the configured member
        // requirement is satisfied by the founder alone.
        var invite = GuildPackets.CreateInvite(player.SessionId, guildName, guildTag);
        foreach (var candidate in candidates)
        {
            await candidate.Send(invite);
        }

        if (!GuildRules.RequiresRecruits(_options))
        {
            // Single member guilds are confirmed immediately, matching eoserv.
            await player.Send(GuildPackets.CreateAddConfirm(""));
        }
    }

    public async Task AcceptGuildCreation(PlayerState player, int inviterPlayerId)
    {
        if (player.Character is null) return;
        if (player.Character.GuildTag is not null) return;

        if (!_creations.TryGetValue(inviterPlayerId, out var creation)) return;

        string? memberName = null;
        var confirmed = false;

        lock (creation)
        {
            if (!creation.Recruits.Contains(player.SessionId))
            {
                creation.Recruits.Add(player.SessionId);
                memberName = player.Character.Name;
                confirmed = GuildRules.HasEnoughMembers(creation.Recruits.Count, _options);
            }
        }

        if (memberName is null) return;

        var leader = world.GetPlayer(inviterPlayerId);
        if (leader is null) return;

        await leader.Send(confirmed
            ? GuildPackets.CreateAddConfirm(memberName)
            : GuildPackets.CreateAdd(memberName));
    }

    public async Task FinishGuildCreation(PlayerState player, int sessionId, string guildTag, string guildName, string description)
    {
        if (player.Character is null || player.CurrentMap is null) return;
        if (player.SessionId != sessionId) return;

        guildTag = guildTag.Trim().ToUpperInvariant();
        guildName = guildName.Trim().ToLowerInvariant();
        description = description.Trim();

        if (!GuildRules.IsValidTag(guildTag, _options)
            || !GuildRules.IsValidName(guildName, _options)
            || !GuildRules.IsValidDescription(description.ToLowerInvariant(), _options))
        {
            await SendGuildReply(player, GuildReply.NotApproved);
            return;
        }

        if (player.Character.GuildTag is not null) return;
        if (inventoryService.GetItemAmount(player.Character, GuildRules.GoldItemId) < _options.Price) return;

        if (!_creations.TryGetValue(player.SessionId, out var creation)) return;
        if (!creation.Tag.Equals(guildTag, StringComparison.Ordinal)) return;

        // The name captured when creation began is authoritative (matches eoserv).
        guildName = creation.Name;

        List<int> recruitIds;
        lock (creation)
        {
            recruitIds = [.. creation.Recruits];
        }

        if (!GuildRules.HasEnoughMembers(recruitIds.Count, _options)) return;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AcornDbContext>();

        if (await GuildExists(db, guildTag, guildName))
        {
            await SendGuildReply(player, GuildReply.Exists);
            return;
        }

        var ranks = GuildRules.ParseRanks(_options.DefaultRanks);
        var guild = new Database.Models.Guild
        {
            Tag = guildTag,
            Name = guildName,
            Description = description,
            Ranks = string.Join(",", ranks),
            Bank = 0,
            CreatedAt = DateTime.UtcNow
        };

        db.Guilds.Add(guild);

        db.GuildMembers.Add(new Database.Models.GuildMember
        {
            CharacterName = player.Character.Name!,
            GuildTag = guildTag,
            RankIndex = GuildRules.LeaderRank
        });

        // Never exceed the configured member cap, even if more players accepted the invite.
        var maxRecruits = Math.Max(0, _options.MaxMembers - 1);
        var recruitPlayers = new List<PlayerState>();
        foreach (var recruitId in recruitIds.Take(maxRecruits))
        {
            var recruitPlayer = player.CurrentMap.Players.Values.FirstOrDefault(p => p.SessionId == recruitId);
            if (recruitPlayer?.Character is not null && recruitPlayer.Character.GuildTag is null)
            {
                recruitPlayers.Add(recruitPlayer);
                db.GuildMembers.Add(new Database.Models.GuildMember
                {
                    CharacterName = recruitPlayer.Character.Name!,
                    GuildTag = guildTag,
                    RankIndex = GuildRules.NewMemberRank
                });
            }
        }

        await db.SaveChangesAsync();

        inventoryService.TryRemoveItem(player.Character, GuildRules.GoldItemId, _options.Price);

        player.Character.GuildTag = guildTag;
        player.Character.GuildName = guildName;
        player.Character.GuildRankIndex = GuildRules.LeaderRank;
        player.Character.GuildRankName = GuildRules.GetRankName(ranks, GuildRules.LeaderRank);

        await player.Send(new GuildCreateServerPacket
        {
            LeaderPlayerId = player.SessionId,
            GuildTag = guildTag,
            GuildName = guildName,
            RankName = GuildRules.GetRankName(ranks, GuildRules.LeaderRank),
            GoldAmount = inventoryService.GetItemAmount(player.Character, GuildRules.GoldItemId)
        });

        var agreePacket = new GuildAgreeServerPacket
        {
            RecruiterId = player.SessionId,
            GuildTag = guildTag,
            GuildName = guildName,
            RankName = GuildRules.GetRankName(ranks, GuildRules.NewMemberRank)
        };

        foreach (var recruit in recruitPlayers)
        {
            recruit.Character!.GuildTag = guildTag;
            recruit.Character.GuildName = guildName;
            recruit.Character.GuildRankIndex = GuildRules.NewMemberRank;
            recruit.Character.GuildRankName = GuildRules.GetRankName(ranks, GuildRules.NewMemberRank);
            await recruit.Send(agreePacket);
        }

        _creations.TryRemove(player.SessionId, out _);

        logger.LogInformation("Guild {GuildTag} ({GuildName}) created by {Player}",
            guildTag, guildName, player.Character.Name);
    }

    public async Task RequestToJoinGuild(PlayerState player, int sessionId, string guildTag, string recruiterName)
    {
        if (player.Character is null || player.CurrentMap is null) return;
        if (player.SessionId != sessionId) return;

        if (player.Character.GuildTag is not null)
        {
            await SendGuildReply(player, GuildReply.AlreadyMember);
            return;
        }

        // Find recruiter on the same map
        var recruiter = player.CurrentMap.Players.Values
            .FirstOrDefault(p => p.Character?.Name?.Equals(recruiterName, StringComparison.OrdinalIgnoreCase) == true);

        if (recruiter is null)
        {
            // Check if online at all
            var onlineRecruiter = world.FindPlayerByName(recruiterName);
            if (onlineRecruiter is null)
            {
                await SendGuildReply(player, GuildReply.RecruiterOffline);
            }
            else
            {
                await SendGuildReply(player, GuildReply.RecruiterNotHere);
            }
            return;
        }

        if (recruiter.Character?.GuildTag is null ||
            !recruiter.Character.GuildTag.Equals(guildTag, StringComparison.OrdinalIgnoreCase))
        {
            await SendGuildReply(player, GuildReply.RecruiterWrongGuild);
            return;
        }

        if (!GuildRules.CanRecruit(recruiter.Character.GuildRankIndex, _options))
        {
            await SendGuildReply(player, GuildReply.NotRecruiter);
            return;
        }

        // Send join request to recruiter
        recruiter.InteractingPlayerId = player.SessionId;
        await recruiter.Send(GuildPackets.JoinRequest(player.SessionId, GuildPackets.Capitalize(player.Character.Name!)));
    }

    public async Task AcceptJoinRequest(PlayerState player, int joiningPlayerId)
    {
        if (player.Character is null) return;
        if (player.InteractingPlayerId != joiningPlayerId) return;

        player.InteractingPlayerId = null;

        if (player.Character.GuildTag is null || !GuildRules.CanRecruit(player.Character.GuildRankIndex, _options)) return;

        var guildTag = player.Character.GuildTag;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AcornDbContext>();

        var guild = await db.Guilds.FirstOrDefaultAsync(g => g.Tag == guildTag);
        if (guild is null) return;

        // Check guild bank for recruit cost
        if (guild.Bank < _options.RecruitCost)
        {
            await SendGuildReply(player, GuildReply.AccountLow);
            return;
        }

        // Enforce the member cap before charging the guild bank.
        var memberCount = await db.GuildMembers.CountAsync(m => m.GuildTag == guildTag);
        if (memberCount >= _options.MaxMembers)
        {
            return;
        }

        var joiningPlayer = world.GetPlayer(joiningPlayerId);
        if (joiningPlayer?.Character is null || joiningPlayer.Character.GuildTag is not null) return;

        guild.Bank -= _options.RecruitCost;

        var ranks = GuildRules.ParseRanks(guild.Ranks);
        var rankName = GuildRules.GetRankName(ranks, GuildRules.NewMemberRank);

        db.GuildMembers.Add(new Database.Models.GuildMember
        {
            CharacterName = joiningPlayer.Character.Name!,
            GuildTag = guildTag,
            RankIndex = GuildRules.NewMemberRank
        });

        await db.SaveChangesAsync();

        joiningPlayer.Character.GuildTag = guildTag;
        joiningPlayer.Character.GuildName = guild.Name;
        joiningPlayer.Character.GuildRankIndex = GuildRules.NewMemberRank;
        joiningPlayer.Character.GuildRankName = rankName;

        await joiningPlayer.Send(new GuildAgreeServerPacket
        {
            RecruiterId = player.SessionId,
            GuildTag = guildTag,
            GuildName = guild.Name,
            RankName = rankName
        });

        await SendGuildReply(player, GuildReply.Accepted);
    }

    public async Task LeaveGuild(PlayerState player, int sessionId)
    {
        if (player.Character is null) return;
        if (player.SessionId != sessionId) return;
        if (player.Character.GuildTag is null) return;

        var guildTag = player.Character.GuildTag;

        // If leader, check if they're the last leader
        if (GuildRules.IsLeader(player.Character.GuildRankIndex))
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AcornDbContext>();

            var leaderCount = await db.GuildMembers
                .CountAsync(m => m.GuildTag == guildTag && m.RankIndex == GuildRules.LeaderRank);

            if (leaderCount <= 1)
            {
                // Can't leave - last leader. Re-send guild info to keep client in sync.
                await player.Send(new GuildAgreeServerPacket
                {
                    RecruiterId = player.SessionId,
                    GuildTag = guildTag,
                    GuildName = player.Character.GuildName ?? "",
                    RankName = player.Character.GuildRankName ?? ""
                });

                await player.Send(new GuildAcceptServerPacket
                {
                    Rank = player.Character.GuildRankIndex
                });

                return;
            }
        }

        await RemoveFromGuild(player);
    }

    public async Task KickFromGuild(PlayerState player, int sessionId, string memberName)
    {
        if (player.Character is null) return;
        if (player.SessionId != sessionId) return;
        if (player.Character.GuildTag is null) return;
        if (!GuildRules.CanKick(player.Character.GuildRankIndex, _options)) return;

        var guildTag = player.Character.GuildTag;
        var target = world.FindPlayerByName(memberName);

        if (target?.Character is null)
        {
            // TODO: offline kick
            return;
        }

        if (target.Character.GuildTag != guildTag)
        {
            await SendGuildReply(player, GuildReply.RemoveNotMember);
            return;
        }

        // Can only kick members of a strictly lower rank.
        if (target.Character.GuildRankIndex <= player.Character.GuildRankIndex)
        {
            await SendGuildReply(player, GuildReply.RemoveLeader);
            return;
        }

        await RemoveFromGuild(target, sendKickPacket: true);
        await SendGuildReply(player, GuildReply.Removed);
    }

    public async Task DepositGuildGold(PlayerState player, int sessionId, int amount)
    {
        if (player.Character is null) return;
        if (player.SessionId != sessionId) return;
        if (player.InteractingNpcIndex is null) return;
        if (player.Character.GuildTag is null) return;
        if (amount < 0) return;

        var guildTag = player.Character.GuildTag;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AcornDbContext>();

        var guild = await db.Guilds.FirstOrDefaultAsync(g => g.Tag == guildTag);
        if (guild is null) return;

        // Work out the real deposit before touching the player's gold so nothing can be lost.
        var depositAmount = GuildRules.CalculateDeposit(
            amount,
            inventoryService.GetItemAmount(player.Character, GuildRules.GoldItemId),
            guild.Bank,
            _options);

        if (depositAmount <= 0) return;

        if (!inventoryService.TryRemoveItem(player.Character, GuildRules.GoldItemId, depositAmount)) return;

        guild.Bank += depositAmount;
        await db.SaveChangesAsync();

        await player.Send(new GuildBuyServerPacket
        {
            GoldAmount = inventoryService.GetItemAmount(player.Character, GuildRules.GoldItemId)
        });
    }

    public async Task UpdateGuildDescription(PlayerState player, int sessionId, string description)
    {
        if (player.Character is null) return;
        if (player.SessionId != sessionId) return;
        if (player.InteractingNpcIndex is null) return;
        if (player.Character.GuildTag is null) return;
        if (!GuildRules.CanEdit(player.Character.GuildRankIndex, _options)) return;

        if (!GuildRules.IsValidDescription(description.ToLowerInvariant(), _options)) return;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AcornDbContext>();

        var guild = await db.Guilds.FirstOrDefaultAsync(g => g.Tag == player.Character.GuildTag);
        if (guild is null) return;

        guild.Description = description;
        await db.SaveChangesAsync();

        await SendGuildReply(player, GuildReply.Updated);
    }

    public async Task UpdateGuildRanks(PlayerState player, int sessionId, string[] ranks)
    {
        if (player.Character is null) return;
        if (player.SessionId != sessionId) return;
        if (player.InteractingNpcIndex is null) return;
        if (player.Character.GuildTag is null) return;
        if (!GuildRules.CanEdit(player.Character.GuildRankIndex, _options)) return;

        if (ranks.Length != GuildRules.RankCount || ranks.Any(r => !GuildRules.IsValidRank(r.ToLowerInvariant(), _options))) return;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AcornDbContext>();

        var guild = await db.Guilds.FirstOrDefaultAsync(g => g.Tag == player.Character.GuildTag);
        if (guild is null) return;

        guild.Ranks = string.Join(",", ranks);
        await db.SaveChangesAsync();

        await SendGuildReply(player, GuildReply.RanksUpdated);
    }

    public async Task UpdateMemberRank(PlayerState player, int sessionId, string memberName, int newRank)
    {
        if (player.Character is null) return;
        if (player.SessionId != sessionId) return;
        if (player.InteractingNpcIndex is null) return;
        if (player.Character.GuildTag is null) return;
        if (newRank < 0 || newRank > GuildRules.NewMemberRank) return;

        var guildTag = player.Character.GuildTag;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AcornDbContext>();

        var guild = await db.Guilds.FirstOrDefaultAsync(g => g.Tag == guildTag);
        if (guild is null) return;

        // Find online target
        var target = world.FindPlayerByName(memberName);
        if (target?.Character is null)
        {
            // TODO: offline rank update
            return;
        }

        if (target.Character.GuildTag != guildTag)
        {
            await SendGuildReply(player, GuildReply.RankingNotMember);
            return;
        }

        if (target.Character.GuildRankIndex == GuildRules.LeaderRank)
        {
            await SendGuildReply(player, GuildReply.RankingLeader);
            return;
        }

        if (!GuildRules.CanAssignRank(player.Character.GuildRankIndex, target.Character.GuildRankIndex, newRank, _options))
        {
            await SendGuildReply(player, GuildReply.RankingLeader);
            return;
        }

        var ranks = GuildRules.ParseRanks(guild.Ranks);
        var rankName = GuildRules.GetRankName(ranks, newRank);

        var member = await db.GuildMembers.FirstOrDefaultAsync(m => m.CharacterName == target.Character.Name);
        if (member is null) return;

        member.RankIndex = newRank;
        await db.SaveChangesAsync();

        target.Character.GuildRankIndex = newRank;
        target.Character.GuildRankName = rankName;

        await target.Send(new GuildAcceptServerPacket
        {
            Rank = newRank
        });

        await SendGuildReply(player, GuildReply.Updated);
    }

    public async Task GetGuildMemberList(PlayerState player, int sessionId, string guildIdentity)
    {
        if (player.Character is null) return;
        if (player.SessionId != sessionId) return;
        if (player.InteractingNpcIndex is null) return;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AcornDbContext>();

        var guild = await db.Guilds
            .FirstOrDefaultAsync(g => g.Tag == guildIdentity || g.Name == guildIdentity);

        if (guild is null)
        {
            await SendGuildReply(player, GuildReply.NotFound);
            return;
        }

        var ranks = GuildRules.ParseRanks(guild.Ranks);
        var members = await db.GuildMembers
            .Where(m => m.GuildTag == guild.Tag)
            .OrderBy(m => m.RankIndex)
            .ThenBy(m => m.CharacterName)
            .ToListAsync();

        if (members.Count == 0)
        {
            await SendGuildReply(player, GuildReply.NotFound);
            return;
        }

        await player.Send(new GuildTellServerPacket
        {
            Members = members.Select(m => new GuildMember
            {
                Rank = m.RankIndex,
                Name = m.CharacterName,
                RankName = GuildRules.GetRankName(ranks, m.RankIndex)
            }).ToList()
        });
    }

    public async Task GetGuildInfo(PlayerState player, int sessionId, string guildIdentity)
    {
        if (player.Character is null) return;
        if (player.SessionId != sessionId) return;
        if (player.InteractingNpcIndex is null) return;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AcornDbContext>();

        var guild = await db.Guilds
            .FirstOrDefaultAsync(g => g.Tag == guildIdentity || g.Name == guildIdentity);

        if (guild is null)
        {
            await SendGuildReply(player, GuildReply.NotFound);
            return;
        }

        var ranks = GuildRules.ParseRanks(guild.Ranks);

        var staffMaxRank = Math.Max(
            Math.Max(GuildRules.ToAcornRank(_options.EditRank), GuildRules.ToAcornRank(_options.KickRank)),
            GuildRules.ToAcornRank(_options.RecruitRank));

        var members = await db.GuildMembers
            .Where(m => m.GuildTag == guild.Tag && m.RankIndex <= staffMaxRank)
            .ToListAsync();

        await player.Send(new GuildReportServerPacket
        {
            Tag = guild.Tag,
            Name = guild.Name,
            Description = string.IsNullOrEmpty(guild.Description) ? " " : guild.Description,
            CreateDate = guild.CreatedAt.ToString(_options.DateFormat),
            Wealth = GuildRules.GetWealth(guild.Bank),
            Ranks = GuildRules.PadRanks(ranks),
            Staff = GuildRules.GetStaff(members.Select(m => (m.RankIndex, m.CharacterName)), _options)
        });
    }

    public async Task GetGuildInfoByType(PlayerState player, int sessionId, int infoType)
    {
        if (player.Character is null) return;
        if (player.SessionId != sessionId) return;
        if (player.InteractingNpcIndex is null) return;
        if (player.Character.GuildTag is null) return;

        var guildTag = player.Character.GuildTag;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AcornDbContext>();

        var guild = await db.Guilds.FirstOrDefaultAsync(g => g.Tag == guildTag);
        if (guild is null) return;

        switch (infoType)
        {
            case 1: // Description
                if (!GuildRules.CanEdit(player.Character.GuildRankIndex, _options)) return;
                await player.Send(new GuildTakeServerPacket
                {
                    Description = string.IsNullOrEmpty(guild.Description) ? " " : guild.Description
                });
                break;
            case 2: // Ranks
                if (!GuildRules.CanEdit(player.Character.GuildRankIndex, _options)) return;
                var ranks = GuildRules.ParseRanks(guild.Ranks);
                await player.Send(new GuildRankServerPacket
                {
                    Ranks = [.. ranks]
                });
                break;
            case 3: // Bank
                await player.Send(new GuildSellServerPacket
                {
                    GoldAmount = guild.Bank
                });
                break;
        }
    }

    public async Task DisbandGuild(PlayerState player, int sessionId)
    {
        if (player.Character is null) return;
        if (player.SessionId != sessionId) return;
        if (player.InteractingNpcIndex is null) return;
        if (player.Character.GuildTag is null) return;
        if (!GuildRules.CanDisband(player.Character.GuildRankIndex, _options)) return;

        var guildTag = player.Character.GuildTag;

        // Find all online members and kick them
        var onlineMembers = world.GetAllPlayers()
            .Where(p => p.Character?.GuildTag == guildTag)
            .ToList();

        foreach (var member in onlineMembers)
        {
            member.Character!.GuildTag = null;
            member.Character.GuildName = null;
            member.Character.GuildRankIndex = 0;
            member.Character.GuildRankName = null;

            await member.Send(new GuildKickServerPacket());
        }

        // Delete from DB
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AcornDbContext>();

        var guild = await db.Guilds.Include(g => g.Members).FirstOrDefaultAsync(g => g.Tag == guildTag);
        if (guild is not null)
        {
            db.GuildMembers.RemoveRange(guild.Members);
            db.Guilds.Remove(guild);
            await db.SaveChangesAsync();
        }

        logger.LogInformation("Guild {GuildTag} disbanded by {Player}", guildTag, player.Character.Name);
    }

    public async Task SendGuildMessage(PlayerState player, string message)
    {
        if (player.Character is null) return;
        if (player.Character.GuildTag is null) return;

        var guildTag = player.Character.GuildTag;
        var packet = new TalkRequestServerPacket
        {
            PlayerName = player.Character.Name!,
            Message = message
        };

        var guildPlayers = world.GetAllPlayers()
            .Where(p => p.Character?.GuildTag == guildTag && p.SessionId != player.SessionId);

        foreach (var guildPlayer in guildPlayers)
        {
            await guildPlayer.Send(packet);
        }
    }

    // --- Helper methods ---

    private async Task RemoveFromGuild(PlayerState player, bool sendKickPacket = false)
    {
        if (player.Character is null) return;
        var characterName = player.Character.Name!;

        // Clear in-memory state
        player.Character.GuildTag = null;
        player.Character.GuildName = null;
        player.Character.GuildRankIndex = 0;
        player.Character.GuildRankName = null;

        if (sendKickPacket)
        {
            await player.Send(new GuildKickServerPacket());
        }

        // Remove from DB
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AcornDbContext>();

        var member = await db.GuildMembers.FirstOrDefaultAsync(m => m.CharacterName == characterName);
        if (member is not null)
        {
            db.GuildMembers.Remove(member);
            await db.SaveChangesAsync();
        }
    }

    private static async Task<bool> GuildExists(AcornDbContext db, string tag, string name)
    {
        return await db.Guilds.AnyAsync(g =>
            g.Tag == tag || g.Name.ToLower() == name.ToLower());
    }

    private static Task SendGuildReply(PlayerState player, GuildReply reply)
    {
        return player.Send(GuildPackets.Reply(reply));
    }

    private sealed class GuildCreation(string tag, string name)
    {
        public string Tag { get; } = tag;
        public string Name { get; } = name;
        public List<int> Recruits { get; } = [];
    }
}
