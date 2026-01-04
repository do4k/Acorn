using System.Collections.Concurrent;
using Moffat.EndlessOnline.SDK.Protocol.Net;

namespace Acorn.Net;

/// <summary>
///     Tracks packet processing times for rate limiting.
/// </summary>
public class PacketLog
{
    private readonly ConcurrentDictionary<PacketKey, DateTime> _lastProcessed = new();
    private readonly List<PacketRateLimit> _rateLimits;

    public PacketLog(List<PacketRateLimit>? rateLimits = null)
    {
        _rateLimits = rateLimits ?? PacketRateLimits.DefaultLimits;
    }

    /// <summary>
    ///     Records that a packet was processed at the current time.
    /// </summary>
    public void RecordPacket(PacketAction action, PacketFamily family)
    {
        var key = new PacketKey(action, family);
        _lastProcessed[key] = DateTime.UtcNow;
    }

    /// <summary>
    ///     Checks if a packet should be rate limited based on when it was last processed.
    ///     Returns true if the packet should be blocked (rate limit exceeded).
    /// </summary>
    public bool ShouldRateLimit(PacketAction action, PacketFamily family)
    {
        var rateLimit = _rateLimits.FirstOrDefault(l =>
            l.Action == action && l.Family == family);

        if (rateLimit == null)
        {
            return false; // No rate limit for this packet type
        }

        var key = new PacketKey(action, family);

        if (!_lastProcessed.TryGetValue(key, out var lastProcessed))
        {
            return false; // First time seeing this packet
        }

        var elapsed = DateTime.UtcNow - lastProcessed;
        return elapsed.TotalMilliseconds < rateLimit.LimitMs;
    }

    /// <summary>
    ///     Gets the time when a packet was last processed, if available.
    /// </summary>
    public DateTime? GetLastProcessedTime(PacketAction action, PacketFamily family)
    {
        var key = new PacketKey(action, family);
        return _lastProcessed.TryGetValue(key, out var time) ? time : null;
    }

    /// <summary>
    ///     Clears all recorded packet times. Useful for resetting state.
    /// </summary>
    public void Clear()
    {
        _lastProcessed.Clear();
    }

    private readonly record struct PacketKey(PacketAction Action, PacketFamily Family);
}