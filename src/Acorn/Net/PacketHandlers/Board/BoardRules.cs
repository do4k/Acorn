using Moffat.EndlessOnline.SDK.Protocol.Map;

namespace Acorn.Net.PacketHandlers.Board;

/// <summary>
///     Shared rules for town boards. Client board ids are 0-based: id 0 maps to
///     <see cref="MapTileSpec.Board1" /> and id 7 maps to <see cref="MapTileSpec.Board8" />.
///     The admin board is the last board (eoserv's "AdminBoard" config value of 8, i.e. 0-based id 7).
/// </summary>
public static class BoardRules
{
    /// <summary>The lowest valid client board id (Board1).</summary>
    public const int FirstBoardId = 0;

    /// <summary>The highest valid client board id (Board8).</summary>
    public const int LastBoardId = 7;

    /// <summary>The 0-based id of the admin-only board (eoserv AdminBoard=8).</summary>
    public const int AdminBoardId = 7;

    /// <summary>Maximum number of posts kept/returned for a normal board (eoserv BoardMaxPosts).</summary>
    public const int MaxPosts = 20;

    /// <summary>Maximum number of posts kept/returned for the admin board (eoserv AdminBoardLimit).</summary>
    public const int AdminBoardMaxPosts = 100;

    /// <summary>Maximum subject length (eoserv BoardMaxSubjectLength).</summary>
    public const int MaxSubjectLength = 32;

    /// <summary>Maximum body length (eoserv BoardMaxPostLength).</summary>
    public const int MaxBodyLength = 2048;

    /// <summary>Maximum number of posts a character may have on a board (eoserv BoardMaxUserPosts).</summary>
    public const int MaxUserPosts = 6;

    /// <summary>Maximum number of recent posts a character may have on a board (eoserv BoardMaxRecentPosts).</summary>
    public const int MaxRecentPosts = 2;

    /// <summary>How long a post is considered "recent" (eoserv BoardRecentPostTime).</summary>
    public static readonly TimeSpan RecentPostWindow = TimeSpan.FromMinutes(30);

    /// <summary>The minimum admin level required to open the admin board (eoserv reports=1).</summary>
    public const int AdminBoardAccessLevel = 1;

    /// <summary>Character used to replace the reserved 0xFF byte in post content.</summary>
    public const char ReplacementChar = 'y';

    public static bool IsValidBoardId(int boardId) => boardId is >= FirstBoardId and <= LastBoardId;

    public static bool IsAdminBoard(int boardId) => boardId == AdminBoardId;

    public static bool CanAccessAdminBoard(int adminLevel) => adminLevel >= AdminBoardAccessLevel;

    /// <summary>Maps a 0-based client board id to the tile spec placed on the map, or null if out of range.</summary>
    public static MapTileSpec? GetTileSpec(int boardId) => boardId switch
    {
        0 => MapTileSpec.Board1,
        1 => MapTileSpec.Board2,
        2 => MapTileSpec.Board3,
        3 => MapTileSpec.Board4,
        4 => MapTileSpec.Board5,
        5 => MapTileSpec.Board6,
        6 => MapTileSpec.Board7,
        7 => MapTileSpec.Board8,
        _ => null
    };

    /// <summary>
    ///     Returns the maximum number of posts to load for a board, honouring the larger admin-board limit.
    /// </summary>
    public static int GetPostLimit(int boardId) => IsAdminBoard(boardId) ? AdminBoardMaxPosts : MaxPosts;

    /// <summary>
    ///     Replaces the reserved 0xFF byte with a printable character and truncates to <paramref name="maxLength" />.
    /// </summary>
    public static string SanitizeContent(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var sanitized = value.Replace('\u00FF', ReplacementChar);
        return sanitized.Length > maxLength ? sanitized[..maxLength] : sanitized;
    }

    /// <summary>
    ///     A post may be removed by its author, or by an admin whose level is higher than the author's.
    ///     Everyone else is denied.
    /// </summary>
    public static bool CanRemovePost(int currentAdminLevel, int authorAdminLevel, bool isAuthor)
        => isAuthor || currentAdminLevel > authorAdminLevel;
}
