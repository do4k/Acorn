using System.Net;
using System.Net.Sockets;
using Acorn.Options;
using Microsoft.Extensions.Options;

namespace Acorn.Net;

/// <summary>
///     Enforces a per-IP limit on accepts from the raw TCP listener (issue #138):
///     internet scanners keep the accept loop busy with connections that are closed
///     immediately, and every accept still allocates a session before the handshake
///     fails. Only globally routable sources are counted - loopback, LAN, CGNAT and
///     link-local addresses bypass the limiter, because Docker's userland proxy
///     (IPv6 and connections to the host) delivers those from a single bridge
///     address, where counting them would let unrelated clients share one bucket
///     and throttle each other. The WebSocket listener sits behind Caddy for the
///     same reason and is not limited here.
/// </summary>
public class AcceptRateLimiter(IOptions<ServerOptions> serverOptions, TimeProvider timeProvider)
{
    /// <summary>
    ///     Upper bound on tracked source IPs; once exceeded, expired windows are pruned
    ///     so a scanner rotating addresses cannot grow the table without bound.
    /// </summary>
    private const int MaxTrackedIps = 4096;

    private readonly object _gate = new();
    private readonly Dictionary<IPAddress, Window> _windows = new();

    /// <summary>
    ///     Decides whether a connection from <paramref name="remoteIp"/> may be handed to
    ///     <see cref="ConnectionHandler"/>. Public sources are limited to
    ///     <c>Server:MaxAcceptsPerIp</c> accepts per <c>Server:AcceptWindowSeconds</c>;
    ///     private/local sources are always accepted and never counted.
    /// </summary>
    /// <returns><see langword="true"/> if the connection may proceed; otherwise it must be closed at accept time.</returns>
    public bool ShouldAccept(IPAddress remoteIp)
    {
        var options = serverOptions.Value;
        var maxAccepts = options.MaxAcceptsPerIp;
        if (maxAccepts <= 0)
        {
            return true; // disabled
        }

        if (IsPrivateOrLocal(remoteIp))
        {
            return true;
        }

        var window = TimeSpan.FromSeconds(Math.Max(1, options.AcceptWindowSeconds));
        var now = timeProvider.GetUtcNow();

        lock (_gate)
        {
            PruneExpired(now, window);

            if (!_windows.TryGetValue(remoteIp, out var entry) || now - entry.Start >= window)
            {
                entry = new Window(now);
                _windows[remoteIp] = entry;
            }

            if (entry.Count >= maxAccepts)
            {
                return false;
            }

            entry.Count++;
            return true;
        }
    }

    /// <summary>
    ///     Drops stale entries once the table outgrows <see cref="MaxTrackedIps"/>. Runs
    ///     only under the gate and only when the table is large, keeping the hot path O(1).
    /// </summary>
    private void PruneExpired(DateTimeOffset now, TimeSpan window)
    {
        if (_windows.Count <= MaxTrackedIps)
        {
            return;
        }

        List<IPAddress>? expired = null;
        foreach (var pair in _windows)
        {
            if (now - pair.Value.Start < window)
            {
                continue;
            }

            expired ??= [];
            expired.Add(pair.Key);
        }

        if (expired is null)
        {
            return;
        }

        foreach (var ip in expired)
        {
            _windows.Remove(ip);
        }
    }

    /// <summary>
    ///     Returns <see langword="true"/> for loopback, RFC1918, CGNAT, link-local and
    ///     IPv6 unique-local sources - the addresses Docker's userland proxy and local
    ///     development produce, which are shared by unrelated clients and must not share
    ///     a rate-limit bucket. Unrecognised families are treated the same way so the
    ///     limiter can never lock out clients we cannot classify.
    /// </summary>
    private static bool IsPrivateOrLocal(IPAddress remoteIp)
    {
        if (remoteIp.AddressFamily == AddressFamily.InterNetwork)
        {
            var octets = remoteIp.GetAddressBytes();
            return octets[0] switch
            {
                0 => true,                               // 0.0.0.0/8
                10 => true,                              // 10.0.0.0/8
                100 => (octets[1] & 0xC0) == 0x40,       // 100.64.0.0/10 (CGNAT)
                127 => true,                             // 127.0.0.0/8
                169 => octets[1] == 254,                 // 169.254.0.0/16 (link-local)
                172 => octets[1] >= 16 && octets[1] <= 31, // 172.16.0.0/12
                192 => octets[1] == 168,                 // 192.168.0.0/16
                _ => false,
            };
        }

        if (remoteIp.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return IPAddress.IsLoopback(remoteIp)
                || remoteIp.IsIPv6LinkLocal
                || remoteIp.IsIPv6SiteLocal
                || remoteIp.IsIPv6Multicast
                || (remoteIp.GetAddressBytes()[0] & 0xFE) == 0xFC; // fc00::/7 (unique local)
        }

        return true;
    }

    /// <summary>Accept count for one IP within the current fixed window.</summary>
    private sealed class Window(DateTimeOffset start)
    {
        public DateTimeOffset Start { get; } = start;

        public int Count { get; set; }
    }
}
