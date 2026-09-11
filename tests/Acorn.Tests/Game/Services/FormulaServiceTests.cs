using System.Collections.Concurrent;
using Acorn.Game.Models;
using Acorn.Game.Services;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using NSubstitute;
using Xunit;

namespace Acorn.Tests.Game.Services;

/// <summary>
///     Combat formulas. The critical-hit rule was aligned with eoserv: a critical
///     only happens from behind/side, or on the first hit when CriticalFirstHit is
///     explicitly enabled (it defaults to false).
/// </summary>
public class FormulaServiceTests
{
    private const int RawDamage = 10;

    private readonly FormulaService _sut = new(Substitute.For<IStatCalculator>());

    [Fact]
    public void CalculateDamage_WhenCritical_ShouldApplyOnePointFiveMultiplier()
    {
        _sut.CalculateDamage(RawDamage, 0, critical: false).Should().Be(10);
        _sut.CalculateDamage(RawDamage, 0, critical: true).Should().Be(15);
    }

    [Fact]
    public void CalculateDamageToNpc_WhenNotCritical_ShouldNeverApplyCriticalMultiplier()
    {
        var character = CreateCharacter();
        var npc = CreateNpc();

        for (var i = 0; i < 200; i++)
        {
            var damage = _sut.CalculateDamageToNpc(character, npc, npc.Hp,
                attackingBackOrSide: false, criticalFirstHit: false);

            damage.Should().BeOneOf(0, RawDamage);
        }
    }

    [Fact]
    public void CalculateDamageToNpc_WhenAttackingBackOrSide_ShouldAllowCriticalHits()
    {
        var character = CreateCharacter();
        var npc = CreateNpc();

        var sawCritical = Enumerable.Range(0, 200)
            .Select(_ => _sut.CalculateDamageToNpc(character, npc, npc.Hp, attackingBackOrSide: true))
            .Any(damage => damage == 15);

        sawCritical.Should().BeTrue();
    }

    [Fact]
    public void CalculateDamageToNpc_WhenCriticalFirstHitAndFullHp_ShouldAllowCriticalHits()
    {
        var character = CreateCharacter();
        var npc = CreateNpc();

        var sawCritical = Enumerable.Range(0, 200)
            .Select(_ => _sut.CalculateDamageToNpc(character, npc, npc.Hp, criticalFirstHit: true))
            .Any(damage => damage == 15);

        sawCritical.Should().BeTrue();
    }

    [Fact]
    public void CalculateDamageToNpc_WhenCriticalFirstHitButNotFullHp_ShouldNeverCrit()
    {
        var character = CreateCharacter();
        var npc = CreateNpc();

        for (var i = 0; i < 200; i++)
        {
            var damage = _sut.CalculateDamageToNpc(character, npc, npc.Hp - 1, criticalFirstHit: true);

            damage.Should().BeOneOf(0, RawDamage);
        }
    }

    [Fact]
    public void CalculateDamageToPlayer_WhenCriticalFirstHitAndFullHp_ShouldAllowCriticalHits()
    {
        var attacker = CreateCharacter();
        var target = CreateCharacter();
        target.MaxHp = 100;
        target.Hp = 100;

        var sawCritical = Enumerable.Range(0, 200)
            .Select(_ => _sut.CalculateDamageToPlayer(attacker, target, criticalFirstHit: true))
            .Any(damage => damage == 15);

        sawCritical.Should().BeTrue();
    }

    [Fact]
    public void CalculateDamageToPlayer_WhenCriticalFirstHitButNotFullHp_ShouldNeverCrit()
    {
        var attacker = CreateCharacter();
        var target = CreateCharacter();
        target.MaxHp = 100;
        target.Hp = 99;

        for (var i = 0; i < 200; i++)
        {
            var damage = _sut.CalculateDamageToPlayer(attacker, target, criticalFirstHit: true);

            damage.Should().BeOneOf(0, RawDamage);
        }
    }

    private static Character CreateCharacter()
    {
        return new Character
        {
            Accounts_Username = "testuser",
            Name = "Tester",
            Accuracy = 100,
            Evade = 0,
            Armor = 0,
            MinDamage = RawDamage,
            MaxDamage = RawDamage,
            Hp = 100,
            MaxHp = 100,
            Inventory = new Inventory(new ConcurrentBag<ItemWithAmount>()),
            Bank = new Bank(new ConcurrentBag<ItemWithAmount>()),
            Paperdoll = new Paperdoll(),
            Spells = new Spells(new ConcurrentBag<Spell>())
        };
    }

    private static EnfRecord CreateNpc()
    {
        return new EnfRecord
        {
            Name = "TestNpc",
            Type = NpcType.Passive,
            Hp = 100,
            Evade = 0,
            Armor = 0
        };
    }
}
