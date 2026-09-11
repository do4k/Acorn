using Acorn.Options;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.World.Services.Guild;

/// <summary>
///     Pure guild rules shared by the guild service and tests: validation, permissions, wealth,
///     staff categorisation and bank deposit maths.
/// </summary>
/// <remarks>
///     Acorn stores member ranks as a zero-based index into the guild's rank list
///     (0 = leader/founder, 1 = recruiter, 8 = new member). eoserv's permission configuration
///     uses a one-based rank number (1 = leader, 2 = recruiter, ...), so eoserv rank thresholds
///     are translated into Acorn's rank space by <see cref="ToAcornRank" />.
/// </remarks>
public static class GuildRules
{
    /// <summary>Inventory item id for gold.</summary>
    public const int GoldItemId = 1;

    /// <summary>Acorn rank index of the guild leader/founder.</summary>
    public const int LeaderRank = 0;

    /// <summary>Acorn rank index of the default recruiter rank.</summary>
    public const int RecruiterRank = 1;

    /// <summary>Acorn rank index of the default new member rank.</summary>
    public const int NewMemberRank = 8;

    /// <summary>Total number of guild ranks.</summary>
    public const int RankCount = 9;

    /// <summary>Translates an eoserv one-based rank threshold into Acorn's zero-based rank space.</summary>
    public static int ToAcornRank(int eoservRank) => eoservRank <= 0 ? 0 : eoservRank - 1;

    // --- Validation (matches eoserv GuildManager::Valid*) ---

    public static bool IsValidTag(string tag, GuildOptions options)
    {
        if (tag.Length < options.MinTagLength || tag.Length > options.MaxTagLength)
        {
            return false;
        }

        return tag.All(c => c is >= 'A' and <= 'Z');
    }

    public static bool IsValidName(string name, GuildOptions options)
    {
        if (name.Length < 4 || name.Length > options.MaxNameLength)
        {
            return false;
        }

        return name.All(c => (c is >= 'a' and <= 'z') || c == ' ');
    }

    public static bool IsValidRank(string rank, GuildOptions options)
    {
        if (rank.Length > options.MaxRankLength)
        {
            return false;
        }

        return rank.All(c => (c is >= 'a' and <= 'z') || c == ' ');
    }

    public static bool IsValidDescription(string description, GuildOptions options)
    {
        if (description.Length > options.MaxDescLength)
        {
            return false;
        }

        return description.All(c =>
            (c is >= 'a' and <= 'z')
            || (c is >= '0' and <= '9')
            || c is ' ' or '@' or '_' or '-' or '.');
    }

    // --- Creation flow ---

    /// <summary>True when creating a guild requires recruiting other players first.</summary>
    public static bool RequiresRecruits(GuildOptions options) => options.CreateMembers > 1;

    /// <summary>True when there are enough nearby unguilded players to satisfy the member requirement.</summary>
    public static bool HasEnoughCandidates(int candidateCount, GuildOptions options) =>
        !RequiresRecruits(options) || candidateCount >= options.CreateMembers - 1;

    /// <summary>True when the leader plus accepted recruits satisfy the member requirement.</summary>
    public static bool HasEnoughMembers(int recruitCount, GuildOptions options) =>
        recruitCount + 1 >= options.CreateMembers;

    // --- Permissions ---

    public static bool IsLeader(int rank) => rank == LeaderRank;

    public static bool CanEdit(int rank, GuildOptions options) => rank <= ToAcornRank(options.EditRank);

    public static bool CanRecruit(int rank, GuildOptions options) => rank <= ToAcornRank(options.RecruitRank);

    public static bool CanKick(int rank, GuildOptions options) => rank <= ToAcornRank(options.KickRank);

    public static bool CanDisband(int rank, GuildOptions options) => rank <= ToAcornRank(options.DisbandRank);

    /// <summary>
    ///     Determines whether <paramref name="actorRank" /> may assign <paramref name="newRank" />
    ///     to a member currently at <paramref name="targetRank" />.
    /// </summary>
    public static bool CanAssignRank(int actorRank, int targetRank, int newRank, GuildOptions options)
    {
        if (newRank < 0 || newRank > NewMemberRank)
        {
            return false;
        }

        if (targetRank == LeaderRank)
        {
            return false;
        }

        if (newRank == LeaderRank)
        {
            return actorRank == LeaderRank && options.MultipleFounders;
        }

        if (actorRank == LeaderRank)
        {
            return true;
        }

        // Non-leaders may only manage members of a strictly lower rank.
        if (actorRank >= targetRank)
        {
            return false;
        }

        var allowed = newRank < targetRank
            ? actorRank <= ToAcornRank(options.PromoteRank)
            : newRank > targetRank
                ? actorRank <= ToAcornRank(options.DemoteRank)
                : actorRank <= ToAcornRank(options.PromoteSameRank);

        if (!allowed)
        {
            return false;
        }

        // Can't promote somebody to a rank at or above your own.
        return newRank > actorRank;
    }

    // --- Wealth / staff ---

    /// <summary>
    ///     eoserv reports the raw bank balance as the guild "wealth" string; the client renders it verbatim.
    /// </summary>
    public static string GetWealth(int bank) => bank.ToString();

    /// <summary>
    ///     Builds the staff list the way eoserv does: leaders (rank &lt;= max(EditRank, KickRank))
    ///     are reported as rank 1 with a "(founder)" suffix for the founder, and recruiters
    ///     (rank &lt;= RecruitRank) as rank 2 when enabled.
    /// </summary>
    public static List<GuildStaff> GetStaff(IEnumerable<(int Rank, string Name)> members, GuildOptions options)
    {
        var memberList = members.ToList();
        var leaderRank = Math.Max(ToAcornRank(options.EditRank), ToAcornRank(options.KickRank));

        var staff = memberList
            .Where(m => m.Rank <= leaderRank)
            .OrderBy(m => m.Rank)
            .ThenBy(m => m.Name)
            .Select(m => new GuildStaff
            {
                Rank = 1,
                Name = m.Rank == LeaderRank ? $"{m.Name} (founder)" : m.Name
            })
            .ToList();

        if (options.ShowRecruiters)
        {
            staff.AddRange(memberList
                .Where(m => m.Rank > leaderRank && m.Rank <= ToAcornRank(options.RecruitRank))
                .OrderBy(m => m.Rank)
                .ThenBy(m => m.Name)
                .Select(m => new GuildStaff
                {
                    Rank = 2,
                    Name = m.Name
                }));
        }

        return staff;
    }

    // --- Bank ---

    /// <summary>
    ///     Calculates how much gold should actually be deposited. Returns 0 when the deposit is
    ///     invalid (below the minimum, no gold available, or the bank is full) so callers must not
    ///     deduct anything. The returned amount never exceeds the player's gold or the remaining
    ///     bank capacity, so gold can never be lost.
    /// </summary>
    public static int CalculateDeposit(int requested, int playerGold, int bank, GuildOptions options)
    {
        if (requested <= 0 || playerGold <= 0)
        {
            return 0;
        }

        var remainingCapacity = options.BankMax - bank;
        if (remainingCapacity <= 0)
        {
            return 0;
        }

        var deposit = Math.Min(Math.Min(requested, playerGold), remainingCapacity);

        return deposit >= options.MinDeposit ? deposit : 0;
    }

    // --- Ranks ---

    public static string[] ParseRanks(string ranks)
    {
        var parsed = ranks.Split(',');
        if (parsed.Length == RankCount)
        {
            return parsed;
        }

        var padded = new string[RankCount];
        Array.Copy(parsed, padded, Math.Min(parsed.Length, RankCount));
        for (var i = parsed.Length; i < RankCount; i++)
        {
            padded[i] = "";
        }

        return padded;
    }

    public static string GetRankName(string[] ranks, int rankIndex)
    {
        return rankIndex >= 0 && rankIndex < ranks.Length ? ranks[rankIndex] : "";
    }

    /// <summary>Pads each rank name to four characters, matching eoserv's report format.</summary>
    public static List<string> PadRanks(string[] ranks)
    {
        var result = new List<string>(RankCount);
        for (var i = 0; i < RankCount; i++)
        {
            var rank = i < ranks.Length ? ranks[i] : "";
            result.Add($"{rank,-4}");
        }

        return result;
    }
}
