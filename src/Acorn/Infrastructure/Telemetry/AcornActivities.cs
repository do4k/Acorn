using System.Diagnostics;
using Moffat.EndlessOnline.SDK.Protocol.Net;

namespace Acorn.Infrastructure.Telemetry;

/// <summary>
///     Helpers for the spans emitted through <see cref="AcornActivitySource" />.
/// </summary>
/// <remarks>
///     Every outbound packet is instrumented at the single
///     <see cref="Acorn.Net.PlayerState.Send" /> choke point. Because an activity is
///     started on the caller's <see cref="Activity.Current" /> context, replies and
///     broadcasts nest beneath the span that caused them (the inbound packet handler
///     or the world tick). One player action therefore shows up as one trace whose
///     flame graph contains all of the packets it produced.
/// </remarks>
public static class AcornActivities
{
    public const string NpcDeath = "npc.death";
    public const string LootRoll = "loot.roll";
    public const string ItemDrop = "item.drop";

    /// <summary>
    ///     Starts a span for an outbound packet. The name follows the same
    ///     <c>{family}/{action}</c> shape as inbound packet spans so a flame graph reads
    ///     as a conversation.
    /// </summary>
    public static Activity? StartPacketSend(IPacket packet, int sessionId, string? username, string? characterName)
    {
        var activity = AcornActivitySource.Instance.StartActivity(
            $"send {packet.Family}/{packet.Action}", ActivityKind.Producer);
        if (activity is null)
        {
            return null;
        }

        activity.SetTag("eo.packet.family", packet.Family.ToString());
        activity.SetTag("eo.packet.action", packet.Action.ToString());
        activity.SetTag("eo.packet.type", packet.GetType().Name);
        activity.SetTag("eo.session.id", sessionId);
        activity.SetTag("enduser.id", username);
        activity.SetTag("acorn.character.name", characterName);
        return activity;
    }

    /// <summary>
    ///     Starts a span covering everything that happens as a result of an NPC dying,
    ///     from the kill itself through the loot roll and the resulting packets.
    /// </summary>
    public static Activity? StartNpcDeath(int npcId, string npcName, int mapId, string? characterName)
    {
        var activity = AcornActivitySource.Instance.StartActivity(NpcDeath, ActivityKind.Internal);
        if (activity is null)
        {
            return null;
        }

        activity.SetTag("acorn.npc.id", npcId);
        activity.SetTag("acorn.npc.name", npcName);
        activity.SetTag("acorn.map.id", mapId);
        activity.SetTag("acorn.character.name", characterName);
        return activity;
    }

    /// <summary>
    ///     Starts a span for rolling an NPC's loot table.
    /// </summary>
    public static Activity? StartLootRoll(int npcId)
    {
        var activity = AcornActivitySource.Instance.StartActivity(LootRoll, ActivityKind.Internal);
        activity?.SetTag("acorn.npc.id", npcId);
        return activity;
    }

    /// <summary>
    ///     Starts a span for spawning a ground item. The caller sets
    ///     <c>acorn.item.index</c> once the map assigns one.
    /// </summary>
    public static Activity? StartItemDrop(int itemId, int amount)
    {
        var activity = AcornActivitySource.Instance.StartActivity(ItemDrop, ActivityKind.Internal);
        if (activity is null)
        {
            return null;
        }

        activity.SetTag("acorn.item.id", itemId);
        activity.SetTag("acorn.item.amount", amount);
        return activity;
    }
}
