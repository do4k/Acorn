namespace Acorn.World.Services.Bans;

/// <summary>
///     Well-known prefixes used to build ban-store keys so that the same service can
///     hold account, HDID and IP bans without collisions.
/// </summary>
public static class BanKeys
{
    public static string Username(string username) => $"user:{username.ToLowerInvariant()}";

    public static string Hdid(string hdid) => $"hdid:{hdid.ToLowerInvariant()}";

    public static string Ip(string ip) => $"ip:{ip.ToLowerInvariant()}";
}
