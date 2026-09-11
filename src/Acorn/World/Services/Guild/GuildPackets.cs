using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.World.Services.Guild;

/// <summary>
///     Builds the guild packets used during the creation flow. Kept separate from
///     <see cref="GuildService" /> so the exact wire contents can be unit tested.
/// </summary>
internal static class GuildPackets
{
    /// <summary>Reply with no attached data.</summary>
    public static GuildReplyServerPacket Reply(GuildReply reply) => new()
    {
        ReplyCode = reply,
        ReplyCodeData = null
    };

    /// <summary>Sent to the leader once validation passes so the client opens the creation dialog.</summary>
    public static GuildReplyServerPacket CreateBegin() => Reply(GuildReply.CreateBegin);

    /// <summary>
    ///     Invite sent to nearby unguilded players. <see cref="GuildRequestServerPacket.GuildIdentity" />
    ///     carries the "Name (TAG)" identity shown by the client.
    /// </summary>
    public static GuildRequestServerPacket CreateInvite(int leaderPlayerId, string guildName, string guildTag) => new()
    {
        PlayerId = leaderPlayerId,
        GuildIdentity = $"{Capitalize(guildName.ToLowerInvariant())} ({guildTag.ToUpperInvariant()})"
    };

    /// <summary>A player accepted; more members are still required.</summary>
    public static GuildReplyServerPacket CreateAdd(string name) => new()
    {
        ReplyCode = GuildReply.CreateAdd,
        ReplyCodeData = new GuildReplyServerPacket.ReplyCodeDataCreateAdd { Name = name }
    };

    /// <summary>A player accepted and the required member count has been reached.</summary>
    public static GuildReplyServerPacket CreateAddConfirm(string name) => new()
    {
        ReplyCode = GuildReply.CreateAddConfirm,
        ReplyCodeData = new GuildReplyServerPacket.ReplyCodeDataCreateAddConfirm { Name = name }
    };

    /// <summary>Sent to a recruiter when another player asks to join their guild.</summary>
    public static GuildReplyServerPacket JoinRequest(int playerId, string name) => new()
    {
        ReplyCode = GuildReply.JoinRequest,
        ReplyCodeData = new GuildReplyServerPacket.ReplyCodeDataJoinRequest
        {
            PlayerId = playerId,
            Name = name
        }
    };

    public static string Capitalize(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return s;
        }

        return char.ToUpper(s[0]) + s[1..].ToLower();
    }
}
