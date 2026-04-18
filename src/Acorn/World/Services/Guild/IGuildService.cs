using Acorn.Net;

namespace Acorn.World.Services.Guild;

/// <summary>
///     Service responsible for all guild operations including creation, membership, ranks, and banking.
/// </summary>
public interface IGuildService
{
    /// <summary>Open the guild master NPC dialog, generating a session ID.</summary>
    Task OpenGuildMaster(PlayerState player, int npcIndex);

    /// <summary>Initiate guild creation flow - validates tag/name, checks cost, sends recruit requests.</summary>
    Task CreateGuildRequest(PlayerState player, int sessionId, string guildTag, string guildName);

    /// <summary>A player accepts a guild creation recruit request.</summary>
    Task AcceptGuildCreation(PlayerState player, int inviterPlayerId);

    /// <summary>Confirm and finalize guild creation with recruited players.</summary>
    Task FinishGuildCreation(PlayerState player, int sessionId, string guildTag, string guildName, string description);

    /// <summary>Request to join an existing guild via NPC.</summary>
    Task RequestToJoinGuild(PlayerState player, int sessionId, string guildTag, string recruiterName);

    /// <summary>Recruiter accepts a player's join request.</summary>
    Task AcceptJoinRequest(PlayerState player, int joiningPlayerId);

    /// <summary>Leave current guild.</summary>
    Task LeaveGuild(PlayerState player, int sessionId);

    /// <summary>Kick a member from the guild (leader only).</summary>
    Task KickFromGuild(PlayerState player, int sessionId, string memberName);

    /// <summary>Deposit gold into the guild bank.</summary>
    Task DepositGuildGold(PlayerState player, int sessionId, int amount);

    /// <summary>Update guild description (leader/officer only).</summary>
    Task UpdateGuildDescription(PlayerState player, int sessionId, string description);

    /// <summary>Update guild rank names (leader only).</summary>
    Task UpdateGuildRanks(PlayerState player, int sessionId, string[] ranks);

    /// <summary>Update a member's rank (leader only).</summary>
    Task UpdateMemberRank(PlayerState player, int sessionId, string memberName, int newRank);

    /// <summary>Get guild member list by tag or name.</summary>
    Task GetGuildMemberList(PlayerState player, int sessionId, string guildIdentity);

    /// <summary>Get guild info/report by tag or name.</summary>
    Task GetGuildInfo(PlayerState player, int sessionId, string guildIdentity);

    /// <summary>Get guild description, ranks, or bank info.</summary>
    Task GetGuildInfoByType(PlayerState player, int sessionId, int infoType);

    /// <summary>Disband guild (leader only).</summary>
    Task DisbandGuild(PlayerState player, int sessionId);

    /// <summary>Send guild chat message to all online guild members.</summary>
    Task SendGuildMessage(PlayerState player, string message);
}
