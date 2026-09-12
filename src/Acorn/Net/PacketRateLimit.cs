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
