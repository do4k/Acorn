using Acorn.Game.Services;
using Acorn.Tests.Support;
using Acorn.World.Npc;
using Acorn.World.Services.Npc;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using NSubstitute;
using SdkNpcType = Moffat.EndlessOnline.SDK.Protocol.Pub.NpcType;

namespace Acorn.Tests.World.Services;

/// <summary>
///     Hidden players ($hide) must not be seen or attacked by NPCs (regression for #102).
/// </summary>
public class NpcCombatServiceTests
{
    private static NpcState CreateAggressiveNpc(int x, int y)
    {
        return new NpcState(new EnfRecord
        {
            Name = "Test",
            Type = SdkNpcType.Aggressive,
            BehaviorId = 1
        })
        {
            X = x,
            Y = y,
            Index = 1
        };
    }

    [Test]
    public void TryAttack_WhenAdjacentPlayerIsHidden_ShouldNotAttack()
    {
        var npc = CreateAggressiveNpc(x: 5, y: 5);
        var player = TestFactories.CreatePlayer(1, x: 6, y: 5);
        player.Character!.Hidden = true;

        // A hidden player is ignored even if they previously attacked the NPC.
        npc.AddOpponent(1, damage: 10);

        var result = new NpcCombatService().TryAttack(npc, npcIndex: 1, [player],
            Substitute.For<IFormulaService>());

        result.Should().BeNull();
    }

    [Test]
    public void TryAttack_WhenAdjacentPlayerIsVisible_ShouldAttack()
    {
        var npc = CreateAggressiveNpc(x: 5, y: 5);
        var player = TestFactories.CreatePlayer(1, x: 6, y: 5);

        var result = new NpcCombatService().TryAttack(npc, npcIndex: 1, [player],
            Substitute.For<IFormulaService>());

        result.Should().NotBeNull();
        result!.PlayerId.Should().Be(1);
    }
}
