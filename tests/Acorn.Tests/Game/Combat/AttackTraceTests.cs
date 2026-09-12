using Acorn.World.Services.Combat;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Pub;

namespace Acorn.Tests.Game.Combat;

/// <summary>
///     Attack tracing mirrors eoserv's Map::Attack: walk outward from the attacker in
///     the packet's direction, target the first occupied tile, and stop at an obstacle.
/// </summary>
public class AttackTraceTests
{
    private static readonly Coords Origin = new() { X = 10, Y = 10 };

    [Test]
    public void FindTargetTile_WhenMeleeAndTargetAdjacent_ReturnsTargetTile()
    {
        var target = new Coords { X = 11, Y = 10 };

        var result = AttackTrace.FindTargetTile(
            Origin,
            Direction.Right,
            1,
            coords => coords.Equals(target),
            _ => false);

        result.Should().NotBeNull();
        result!.X.Should().Be(target.X);
        result.Y.Should().Be(target.Y);
    }

    [Test]
    public void FindTargetTile_WhenRangedAndTargetAtMaxDistance_ReturnsTargetTile()
    {
        var target = new Coords { X = 15, Y = 10 };

        var result = AttackTrace.FindTargetTile(
            Origin,
            Direction.Right,
            5,
            coords => coords.Equals(target),
            _ => false);

        result.Should().NotBeNull();
        result!.X.Should().Be(15);
        result.Y.Should().Be(10);
    }

    [Test]
    public void FindTargetTile_WhenTargetBeyondRange_ReturnsNull()
    {
        var target = new Coords { X = 16, Y = 10 };

        var result = AttackTrace.FindTargetTile(
            Origin,
            Direction.Right,
            5,
            coords => coords.Equals(target),
            _ => false);

        result.Should().BeNull();
    }

    [Test]
    public void FindTargetTile_WhenObstacleBeforeTarget_ReturnsNull()
    {
        var target = new Coords { X = 15, Y = 10 };
        var obstacle = new Coords { X = 12, Y = 10 };

        var result = AttackTrace.FindTargetTile(
            Origin,
            Direction.Right,
            5,
            coords => coords.Equals(target),
            coords => coords.Equals(obstacle));

        result.Should().BeNull();
    }

    [Test]
    public void FindTargetTile_WhenTargetOnBlockedTile_ReturnsTargetTile()
    {
        // eoserv checks for an occupant before testing walkability, so a target
        // standing on a blocked tile is still hit.
        var target = new Coords { X = 12, Y = 10 };

        var result = AttackTrace.FindTargetTile(
            Origin,
            Direction.Right,
            5,
            coords => coords.Equals(target),
            coords => coords.Equals(target));

        result.Should().NotBeNull();
        result!.X.Should().Be(12);
    }

    [Test]
    public void FindTargetTile_WhenTargetBehindAttacker_ReturnsNull()
    {
        var target = new Coords { X = 9, Y = 10 };

        var result = AttackTrace.FindTargetTile(
            Origin,
            Direction.Right,
            5,
            coords => coords.Equals(target),
            _ => false);

        result.Should().BeNull();
    }

    [Test]
    public void FindTargetTile_WhenNothingInPath_ReturnsNull()
    {
        var result = AttackTrace.FindTargetTile(
            Origin,
            Direction.Up,
            5,
            _ => false,
            _ => false);

        result.Should().BeNull();
    }

    [Test]
    [Arguments(Direction.Up, 10, 9)]
    [Arguments(Direction.Down, 10, 11)]
    [Arguments(Direction.Left, 9, 10)]
    [Arguments(Direction.Right, 11, 10)]
    public void FindTargetTile_StepsInPacketDirection(Direction direction, int expectedX, int expectedY)
    {
        var result = AttackTrace.FindTargetTile(
            Origin,
            direction,
            1,
            _ => true,
            _ => false);

        result.Should().NotBeNull();
        result!.X.Should().Be(expectedX);
        result.Y.Should().Be(expectedY);
    }

    [Test]
    public void RangedAttack_ReachesFiveTiles_WhileMeleeDoesNot()
    {
        var target = new Coords { X = 15, Y = 10 };
        bool Occupied(Coords coords) => coords.Equals(target);
        bool NotBlocked(Coords _) => false;

        var rangedRange = AttackTrace.GetRange(ItemSubtype.Ranged, 5);
        var meleeRange = AttackTrace.GetRange(ItemSubtype.None, 5);

        AttackTrace.FindTargetTile(Origin, Direction.Right, rangedRange, Occupied, NotBlocked)
            .Should().NotBeNull("a ranged weapon reaches the configured distance");
        AttackTrace.FindTargetTile(Origin, Direction.Right, meleeRange, Occupied, NotBlocked)
            .Should().BeNull("a melee attack only reaches the adjacent tile");
    }

    [Test]
    public void GetRange_WhenRangedWeapon_ReturnsConfiguredDistance()
    {
        AttackTrace.GetRange(ItemSubtype.Ranged, 5).Should().Be(5);
        AttackTrace.GetRange(ItemSubtype.Ranged, 8).Should().Be(8);
    }

    [Test]
    [Arguments(ItemSubtype.None)]
    [Arguments(ItemSubtype.Arrows)]
    [Arguments(ItemSubtype.Wings)]
    [Arguments(null)]
    public void GetRange_WhenNotRanged_ReturnsMeleeDistance(ItemSubtype? subtype)
    {
        AttackTrace.GetRange(subtype, 5).Should().Be(1);
    }
}