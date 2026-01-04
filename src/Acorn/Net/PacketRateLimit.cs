using Moffat.EndlessOnline.SDK.Protocol.Net;

namespace Acorn.Net;

/// <summary>
///     Represents a rate limit for a specific packet type.
/// </summary>
public record PacketRateLimit
{
    public required PacketAction Action { get; init; }
    public required PacketFamily Family { get; init; }
    public required int LimitMs { get; init; }

    public override string ToString()
    {
        return $"{Action}_{Family} ({LimitMs}ms)";
    }
}

/// <summary>
///     Configuration for packet rate limits to prevent spam and DoS attacks.
/// </summary>
public static class PacketRateLimits
{
    /// <summary>
    ///     Default rate limits for common packet types that are prone to spam.
    ///     Values are in milliseconds between allowed packets.
    /// </summary>
    public static readonly List<PacketRateLimit> DefaultLimits =
    [
        // Talk packets - prevent chat spam
        new() { Action = PacketAction.Report, Family = PacketFamily.Talk, LimitMs = 500 },
        new() { Action = PacketAction.Tell, Family = PacketFamily.Talk, LimitMs = 500 },
        new() { Action = PacketAction.Request, Family = PacketFamily.Talk, LimitMs = 500 },
        new() { Action = PacketAction.Open, Family = PacketFamily.Talk, LimitMs = 500 },
        new() { Action = PacketAction.Msg, Family = PacketFamily.Talk, LimitMs = 500 },
        new() { Action = PacketAction.Admin, Family = PacketFamily.Talk, LimitMs = 500 },
        new() { Action = PacketAction.Announce, Family = PacketFamily.Talk, LimitMs = 500 },

        // Attack packets - prevent attack spam
        new() { Action = PacketAction.Use, Family = PacketFamily.Attack, LimitMs = 100 },

        // Trade packets
        new() { Action = PacketAction.Request, Family = PacketFamily.Trade, LimitMs = 1000 },
        new() { Action = PacketAction.Accept, Family = PacketFamily.Trade, LimitMs = 1000 },
        new() { Action = PacketAction.Close, Family = PacketFamily.Trade, LimitMs = 1000 },

        // Item usage
        new() { Action = PacketAction.Use, Family = PacketFamily.Item, LimitMs = 100 },
        new() { Action = PacketAction.Drop, Family = PacketFamily.Item, LimitMs = 100 },
        new() { Action = PacketAction.Junk, Family = PacketFamily.Item, LimitMs = 100 },

        // Spell casting
        new() { Action = PacketAction.Request, Family = PacketFamily.Spell, LimitMs = 100 },

        // Shop interactions
        new() { Action = PacketAction.Buy, Family = PacketFamily.Shop, LimitMs = 100 },
        new() { Action = PacketAction.Sell, Family = PacketFamily.Shop, LimitMs = 100 },

        // Emotes
        new() { Action = PacketAction.Report, Family = PacketFamily.Emote, LimitMs = 500 },

        // Face changes
        new() { Action = PacketAction.Player, Family = PacketFamily.Face, LimitMs = 500 },

        // Sit/stand
        new() { Action = PacketAction.Request, Family = PacketFamily.Sit, LimitMs = 500 },
        new() { Action = PacketAction.Close, Family = PacketFamily.Sit, LimitMs = 500 }
    ];
}