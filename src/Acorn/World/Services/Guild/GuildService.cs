using System.Collections.Concurrent;
using Acorn.Database;
using Acorn.Extensions;
using Acorn.Game.Services;
using Acorn.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.World.Services.Guild;

public class GuildService(
    IServiceScopeFactory scopeFactory,
    IWorldQueries world,
    IInventoryService inventoryService,
    ILogger<GuildService> logger) : IGuildService
{
    private const int GuildCreateCost = 50000;
    private const int MinPlayersForCreation = 1; // Configurable; reoserv default is 10
    private const int GoldItemId = 1;
    private const int MaxBankGold = 2_000_000_000;
    private const int RecruitCost = 1000;
    private const int MinDeposit = 1000;

    private static readonly string[] DefaultRanks =
    [
        "Leader", "Recruiter", "", "", "", "", "", "", "New Member"
    ];

    // Track guild creation recruits per player session
    private readonly ConcurrentDictionary<int, List<int>> _creationRecruits = new();

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
        guildName = guildName.Trim();

        if (!ValidateGuildTag(guildTag) || !ValidateGuildName(guildName))
        {
            await SendGuildReply(player, GuildReply.NotApproved);
            return;
        }

        if (player.Character.GuildTag is not null)
        {
            return;
        }

        if (inventoryService.GetItemAmount(player.Character, GoldItemId) < GuildCreateCost)
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

        _creationRecruits[player.SessionId] = [];

        if (MinPlayersForCreation <= 1)
        {
            await SendGuildReply(player, GuildReply.CreateAddConfirm);
        }
        else
        {
            var unguildedCount = player.CurrentMap.Players.Values
                .Count(p => p.Character?.GuildTag is null && p.SessionId != player.SessionId);

            if (unguildedCount + 1 < MinPlayersForCreation)
            {
                await SendGuildReply(player, GuildReply.NoCandidates);
                return;
            }

            await SendGuildReply(player, GuildReply.CreateBegin);

            // Send create requests to unguilded players on the map
            var guildIdentity = $"{Capitalize(player.Character.Name!)} ({guildTag})";
            var requestPacket = new GuildRequestServerPacket
            {
                PlayerId = player.SessionId,
                GuildIdentity = guildIdentity
            };

            foreach (var other in player.CurrentMap.Players.Values
                .Where(p => p.SessionId != player.SessionId && p.Character?.GuildTag is null))
            {
                await other.Send(requestPacket);
            }
        }
    }

    public Task AcceptGuildCreation(PlayerState player, int inviterPlayerId)
    {
        if (player.Character is null) return Task.CompletedTask;
        if (player.Character.GuildTag is not null) return Task.CompletedTask;

        if (_creationRecruits.TryGetValue(inviterPlayerId, out var members))
        {
            if (!members.Contains(player.SessionId))
            {
                members.Add(player.SessionId);
            }
        }

        return Task.CompletedTask;
    }

    public async Task FinishGuildCreation(PlayerState player, int sessionId, string guildTag, string guildName, string description)
    {
        if (player.Character is null || player.CurrentMap is null) return;
        if (player.SessionId != sessionId) return;

        guildTag = guildTag.Trim().ToUpperInvariant();
        guildName = guildName.Trim();
        description = description.Trim();

        if (!ValidateGuildTag(guildTag) || !ValidateGuildName(guildName) || !ValidateDescription(description))
        {
            await SendGuildReply(player, GuildReply.NotApproved);
            return;
        }

        if (player.Character.GuildTag is not null) return;
        if (inventoryService.GetItemAmount(player.Character, GoldItemId) < GuildCreateCost) return;

        _creationRecruits.TryGetValue(player.SessionId, out var recruitIds);
        var recruitCount = (recruitIds?.Count ?? 0) + 1; // +1 for the leader
        if (recruitCount < MinPlayersForCreation) return;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AcornDbContext>();

        if (await GuildExists(db, guildTag, guildName))
        {
            await SendGuildReply(player, GuildReply.Exists);
            return;
        }

        // Create guild in DB
        var ranks = string.Join(",", DefaultRanks);
        var guild = new Database.Models.Guild
        {
            Tag = guildTag,
            Name = guildName,
            Description = description,
            Ranks = ranks,
            Bank = 0,
            CreatedAt = DateTime.UtcNow
        };

        db.Guilds.Add(guild);

        // Add leader as member
        db.GuildMembers.Add(new Database.Models.GuildMember
        {
            CharacterName = player.Character.Name!,
            GuildTag = guildTag,
            RankIndex = 0
        });

        // Add recruits as members
        var recruitPlayers = new List<PlayerState>();
        if (recruitIds is not null)
        {
            foreach (var recruitId in recruitIds)
            {
                var recruitPlayer = player.CurrentMap.Players.Values.FirstOrDefault(p => p.SessionId == recruitId);
                if (recruitPlayer?.Character is not null && recruitPlayer.Character.GuildTag is null)
                {
                    recruitPlayers.Add(recruitPlayer);
                    db.GuildMembers.Add(new Database.Models.GuildMember
                    {
                        CharacterName = recruitPlayer.Character.Name!,
                        GuildTag = guildTag,
                        RankIndex = 8
                    });
                }
            }
        }

        await db.SaveChangesAsync();

        // Deduct gold from leader
        inventoryService.TryRemoveItem(player.Character, GoldItemId, GuildCreateCost);

        // Update leader's character
        player.Character.GuildTag = guildTag;
        player.Character.GuildName = guildName;
        player.Character.GuildRankIndex = 0;
        player.Character.GuildRankName = DefaultRanks[0];

        await player.Send(new GuildCreateServerPacket
        {
            LeaderPlayerId = player.SessionId,
            GuildTag = guildTag,
            GuildName = guildName,
            RankName = DefaultRanks[0],
            GoldAmount = inventoryService.GetItemAmount(player.Character, GoldItemId)
        });

        // Notify recruits
        var agreePacket = new GuildAgreeServerPacket
        {
            RecruiterId = player.SessionId,
            GuildTag = guildTag,
            GuildName = guildName,
            RankName = DefaultRanks[8]
        };

        foreach (var recruit in recruitPlayers)
        {
            recruit.Character!.GuildTag = guildTag;
            recruit.Character.GuildName = guildName;
            recruit.Character.GuildRankIndex = 8;
            recruit.Character.GuildRankName = DefaultRanks[8];
            await recruit.Send(agreePacket);
        }

        _creationRecruits.TryRemove(player.SessionId, out _);

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

        if (recruiter.Character.GuildRankIndex > 1)
        {
            await SendGuildReply(player, GuildReply.NotRecruiter);
            return;
        }

        // Send join request to recruiter
        recruiter.InteractingPlayerId = player.SessionId;
        await recruiter.Send(new GuildReplyServerPacket
        {
            ReplyCode = GuildReply.JoinRequest,
            ReplyCodeData = new GuildReplyServerPacket.ReplyCodeDataJoinRequest
            {
                PlayerId = player.SessionId,
                Name = Capitalize(player.Character.Name!)
            }
        });
    }

    public async Task AcceptJoinRequest(PlayerState player, int joiningPlayerId)
    {
        if (player.Character is null) return;
        if (player.InteractingPlayerId != joiningPlayerId) return;

        player.InteractingPlayerId = null;

        if (player.Character.GuildTag is null || player.Character.GuildRankIndex > 1) return;

        var guildTag = player.Character.GuildTag;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AcornDbContext>();

        var guild = await db.Guilds.FirstOrDefaultAsync(g => g.Tag == guildTag);
        if (guild is null) return;

        // Check guild bank for recruit cost
        if (guild.Bank < RecruitCost)
        {
            await SendGuildReply(player, GuildReply.AccountLow);
            return;
        }

        guild.Bank -= RecruitCost;

        var ranks = ParseRanks(guild.Ranks);
        var rankName = ranks.Length > 8 ? ranks[8] : "New Member";

        var joiningPlayer = world.GetPlayer(joiningPlayerId);
        if (joiningPlayer?.Character is null || joiningPlayer.Character.GuildTag is not null) return;

        // Add to DB
        db.GuildMembers.Add(new Database.Models.GuildMember
        {
            CharacterName = joiningPlayer.Character.Name!,
            GuildTag = guildTag,
            RankIndex = 8
        });

        await db.SaveChangesAsync();

        // Update in-memory
        joiningPlayer.Character.GuildTag = guildTag;
        joiningPlayer.Character.GuildName = guild.Name;
        joiningPlayer.Character.GuildRankIndex = 8;
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

        // If leader (rank 0 or 1), check if they're the last leader
        if (player.Character.GuildRankIndex <= 1)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AcornDbContext>();

            var leaderCount = await db.GuildMembers
                .CountAsync(m => m.GuildTag == guildTag && m.RankIndex <= 1);

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
                    Rank = player.Character.GuildRankIndex + 1
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
        if (player.Character.GuildRankIndex > 0) return; // Only leader can kick

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

        if (target.Character.GuildRankIndex == 0)
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
        if (amount < MinDeposit) return;

        var guildTag = player.Character.GuildTag;

        // Clamp to what the player actually has
        var actualAmount = Math.Min(amount, inventoryService.GetItemAmount(player.Character, GoldItemId));
        if (actualAmount <= 0) return;

        inventoryService.TryRemoveItem(player.Character, GoldItemId, actualAmount);

        await player.Send(new GuildBuyServerPacket
        {
            GoldAmount = inventoryService.GetItemAmount(player.Character, GoldItemId)
        });

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AcornDbContext>();

        var guild = await db.Guilds.FirstOrDefaultAsync(g => g.Tag == guildTag);
        if (guild is null) return;

        if (guild.Bank >= MaxBankGold) return;

        var depositAmount = Math.Min(MaxBankGold - guild.Bank, actualAmount);
        guild.Bank += depositAmount;
        await db.SaveChangesAsync();
    }

    public async Task UpdateGuildDescription(PlayerState player, int sessionId, string description)
    {
        if (player.Character is null) return;
        if (player.SessionId != sessionId) return;
        if (player.InteractingNpcIndex is null) return;
        if (player.Character.GuildTag is null) return;
        if (player.Character.GuildRankIndex > 1) return;

        if (!ValidateDescription(description)) return;

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
        if (player.Character.GuildRankIndex > 1) return;

        if (ranks.Length != 9 || ranks.Any(r => !ValidateRankName(r))) return;

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
        if (player.Character.GuildRankIndex != 0) return; // Only leader
        if (newRank < 1 || newRank > 9) return;

        var guildTag = player.Character.GuildTag;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AcornDbContext>();

        var guild = await db.Guilds.FirstOrDefaultAsync(g => g.Tag == guildTag);
        if (guild is null) return;

        var ranks = ParseRanks(guild.Ranks);
        var rankName = (newRank - 1 >= 0 && newRank - 1 < ranks.Length) ? ranks[newRank - 1] : "";

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

        if (target.Character.GuildRankIndex == 0)
        {
            await SendGuildReply(player, GuildReply.RankingLeader);
            return;
        }

        // Update in DB
        var member = await db.GuildMembers.FirstOrDefaultAsync(m => m.CharacterName == target.Character.Name);
        if (member is null) return;

        // reoserv stores rank 1-9 in packet but 0-8 in DB. The packet rank is 1-indexed.
        member.RankIndex = newRank - 1;
        await db.SaveChangesAsync();

        // Update in-memory
        target.Character.GuildRankIndex = newRank - 1;
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

        var ranks = ParseRanks(guild.Ranks);
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
                Rank = m.RankIndex + 1,
                Name = m.CharacterName,
                RankName = (m.RankIndex >= 0 && m.RankIndex < ranks.Length) ? ranks[m.RankIndex] : ""
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

        var ranks = ParseRanks(guild.Ranks);

        // Get staff (rank 0-2)
        var staff = await db.GuildMembers
            .Where(m => m.GuildTag == guild.Tag && m.RankIndex <= 2)
            .OrderBy(m => m.RankIndex)
            .ToListAsync();

        var wealth = guild.Bank switch
        {
            < 2000 => "bankrupt",
            < 10000 => "poor",
            < 50000 => "normal",
            < 100000 => "wealthy",
            _ => "very wealthy"
        };

        await player.Send(new GuildReportServerPacket
        {
            Tag = guild.Tag,
            Name = guild.Name,
            Description = string.IsNullOrEmpty(guild.Description) ? " " : guild.Description,
            CreateDate = guild.CreatedAt.ToString("yyyy-MM-dd"),
            Wealth = wealth,
            Ranks = PadRanks(ranks),
            Staff = staff.Select(s => new GuildStaff
            {
                Rank = s.RankIndex + 1,
                Name = s.CharacterName
            }).ToList()
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
                await player.Send(new GuildTakeServerPacket
                {
                    Description = string.IsNullOrEmpty(guild.Description) ? " " : guild.Description
                });
                break;
            case 2: // Ranks
                var ranks = ParseRanks(guild.Ranks);
                await player.Send(new GuildRankServerPacket
                {
                    Ranks = PadRanks(ranks)
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
        if (player.Character.GuildRankIndex != 0) return; // Only leader

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
        var guildTag = player.Character.GuildTag;

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

    private static bool ValidateGuildTag(string tag)
    {
        return tag.Length == 3 && tag.All(char.IsLetterOrDigit);
    }

    private static bool ValidateGuildName(string name)
    {
        return name.Length >= 1 && name.Length <= 100;
    }

    private static bool ValidateDescription(string description)
    {
        return description.Length <= 500;
    }

    private static bool ValidateRankName(string rank)
    {
        return rank.Length <= 50;
    }

    private static string[] ParseRanks(string ranks)
    {
        var parsed = ranks.Split(',');
        if (parsed.Length < 9)
        {
            var padded = new string[9];
            Array.Copy(parsed, padded, parsed.Length);
            for (var i = parsed.Length; i < 9; i++) padded[i] = "";
            return padded;
        }
        return parsed;
    }

    private static List<string> PadRanks(string[] ranks)
    {
        var result = new List<string>(9);
        for (var i = 0; i < 9; i++)
        {
            var rank = i < ranks.Length ? ranks[i] : "";
            result.Add($"{rank,-4}");
        }
        return result;
    }

    private static string Capitalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpper(s[0]) + s[1..].ToLower();
    }

    private static async Task SendGuildReply(PlayerState player, GuildReply reply)
    {
        await player.Send(new GuildReplyServerPacket
        {
            ReplyCode = reply,
            ReplyCodeData = null
        });
    }
}
