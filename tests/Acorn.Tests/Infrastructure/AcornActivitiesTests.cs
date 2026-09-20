using System.Diagnostics;
using Acorn.Infrastructure.Telemetry;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;

namespace Acorn.Tests.Infrastructure;

/// <summary>
///     Verifies that the outbound packet and NPC-death spans parent onto the activity
///     that caused them, which is what links a cascade of packets into one flame graph.
/// </summary>
public class AcornActivitiesTests
{
    [Test]
    public void StartPacketSend_ShouldNestUnderCurrentActivity_AndTagPacket()
    {
        using var listener = CreateListener();
        ActivitySource.AddActivityListener(listener);

        using var root = AcornActivitySource.Instance.StartActivity("packet Player/Attack", ActivityKind.Server);
        using var send = AcornActivities.StartPacketSend(new NpcSpecServerPacket(), 7, "alice", "Alice");

        send.Should().NotBeNull();
        send!.ParentSpanId.Should().Be(root!.SpanId);
        send.TraceId.Should().Be(root.TraceId);
        send.GetTagItem("eo.packet.family").Should().Be("Npc");
        send.GetTagItem("eo.packet.type").Should().Be(nameof(NpcSpecServerPacket));
        send.GetTagItem("eo.session.id").Should().Be(7);
        send.GetTagItem("enduser.id").Should().Be("alice");
        send.GetTagItem("acorn.character.name").Should().Be("Alice");
    }

    [Test]
    public void NpcDeathSpans_ShouldShareTheTraceOfTheKillingPacket()
    {
        using var listener = CreateListener();
        ActivitySource.AddActivityListener(listener);

        using var packet = AcornActivitySource.Instance.StartActivity("packet Player/Attack", ActivityKind.Server);
        using var death = AcornActivities.StartNpcDeath(5, "Rat", 3, "Alice");
        var deathSpanId = death!.SpanId;

        death.GetTagItem("acorn.npc.id").Should().Be(5);
        death.GetTagItem("acorn.npc.name").Should().Be("Rat");

        // Scoped like the production code: the roll completes before the drop starts, so
        // both are siblings under the death span rather than the drop nesting in the roll.
        using (var loot = AcornActivities.StartLootRoll(5))
        {
            loot!.ParentSpanId.Should().Be(deathSpanId);
        }

        using (var drop = AcornActivities.StartItemDrop(42, 2))
        {
            drop!.ParentSpanId.Should().Be(deathSpanId);
            drop.GetTagItem("acorn.item.id").Should().Be(42);
            drop.GetTagItem("acorn.item.amount").Should().Be(2);
            drop.TraceId.Should().Be(packet!.TraceId);
        }

        death.TraceId.Should().Be(packet!.TraceId);
    }

    private static ActivityListener CreateListener()
    {
        return new ActivityListener
        {
            ShouldListenTo = source => source.Name == AcornActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded
        };
    }
}
