using System.Collections.Concurrent;
using Acorn.Database.Repository;
using Acorn.Game.Models;
using Acorn.Game.Services;
using Acorn.Tests.TestHelpers;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using NSubstitute;

namespace Acorn.Tests.Game.Services;

/// <summary>
///     Formula and level-up tests. The critical-hit rule was aligned with eoserv: a
///     critical only happens from behind/side, or on the first hit when CriticalFirstHit
///     is explicitly enabled (it defaults to false). Level-up grants configurable
///     stat/skill points.
/// </summary>
public class FormulaServiceTests
{
    private const int RawDamage = 10;

    private readonly Ecf _ecf = GameTestFactory.Ecf();

    private static FormulaService CreateSut(int statPerLevel = 3, int skillPerLevel = 4)
    {
        var dataFileRepository = Substitute.For<IDataFileRepository>();
        dataFileRepository.Eif.Returns(new Eif());

        return new FormulaService(
            new StatCalculator(dataFileRepository),
            GameTestFactory.ServerOptions(statPerLevel: statPerLevel, skillPerLevel: skillPerLevel));
    }

    [Test]
    public void CalculateDamage_WhenCritical_ShouldApplyOnePointFiveMultiplier()
    {
        var sut = CreateSut();

        sut.CalculateDamage(RawDamage, 0, critical: false).Should().Be(10);
        sut.CalculateDamage(RawDamage, 0, critical: true).Should().Be(15);
    }

    [Test]
    public void CalculateDamageToNpc_WhenNotCritical_ShouldNeverApplyCriticalMultiplier()
    {
        var sut = CreateSut();
        var character = CreateCharacter();
        var npc = CreateNpc();

        for (var i = 0; i < 200; i++)
        {
            var damage = sut.CalculateDamageToNpc(character, npc, npc.Hp,
                attackingBackOrSide: false, criticalFirstHit: false);

            damage.Should().BeOneOf(0, RawDamage);
        }
    }

    [Test]
    public void CalculateDamageToNpc_WhenAttackingBackOrSide_ShouldAllowCriticalHits()
    {
        var sut = CreateSut();
        var character = CreateCharacter();
        var npc = CreateNpc();

        var sawCritical = Enumerable.Range(0, 200)
            .Select(_ => sut.CalculateDamageToNpc(character, npc, npc.Hp, attackingBackOrSide: true))
            .Any(damage => damage == 15);

        sawCritical.Should().BeTrue();
    }

    [Test]
    public void CalculateDamageToNpc_WhenCriticalFirstHitAndFullHp_ShouldAllowCriticalHits()
    {
        var sut = CreateSut();
        var character = CreateCharacter();
        var npc = CreateNpc();

        var sawCritical = Enumerable.Range(0, 200)
            .Select(_ => sut.CalculateDamageToNpc(character, npc, npc.Hp, criticalFirstHit: true))
            .Any(damage => damage == 15);

        sawCritical.Should().BeTrue();
    }

    [Test]
    public void CalculateDamageToNpc_WhenCriticalFirstHitButNotFullHp_ShouldNeverCrit()
    {
        var sut = CreateSut();
        var character = CreateCharacter();
        var npc = CreateNpc();

        for (var i = 0; i < 200; i++)
        {
            var damage = sut.CalculateDamageToNpc(character, npc, npc.Hp - 1, criticalFirstHit: true);

            damage.Should().BeOneOf(0, RawDamage);
        }
    }

    [Test]
    public void CalculateDamageToPlayer_WhenCriticalFirstHitAndFullHp_ShouldAllowCriticalHits()
    {
        var sut = CreateSut();
        var attacker = CreateCharacter();
        var target = CreateCharacter();
        target.MaxHp = 100;
        target.Hp = 100;

        var sawCritical = Enumerable.Range(0, 200)
            .Select(_ => sut.CalculateDamageToPlayer(attacker, target, criticalFirstHit: true))
            .Any(damage => damage == 15);

        sawCritical.Should().BeTrue();
    }

    [Test]
    public void CalculateDamageToPlayer_WhenCriticalFirstHitButNotFullHp_ShouldNeverCrit()
    {
        var sut = CreateSut();
        var attacker = CreateCharacter();
        var target = CreateCharacter();
        target.MaxHp = 100;
        target.Hp = 99;

        for (var i = 0; i < 200; i++)
        {
            var damage = sut.CalculateDamageToPlayer(attacker, target, criticalFirstHit: true);

            damage.Should().BeOneOf(0, RawDamage);
        }
    }

    [Test]
    public void LevelUp_WhenEnoughExperience_ShouldGrantDefaultPoints()
    {
        var character = GameTestFactory.Character();
        character.Exp = 100_000;
        var sut = CreateSut();

        var level = sut.LevelUp(character, _ecf);

        level.Should().Be(1);
        character.StatPoints.Should().Be(3, "default StatPerLevel is 3");
        character.SkillPoints.Should().Be(4, "default SkillPerLevel is 4 (matches eoserv)");
    }

    [Test]
    public void LevelUp_ShouldGrantConfiguredPoints()
    {
        var character = GameTestFactory.Character();
        character.Exp = 100_000;
        var sut = CreateSut(statPerLevel: 5, skillPerLevel: 2);

        sut.LevelUp(character, _ecf);

        character.StatPoints.Should().Be(5);
        character.SkillPoints.Should().Be(2);
    }

    [Test]
    public void LevelUp_WhenNotEnoughExperience_ShouldNotLevelOrGrantPoints()
    {
        var character = GameTestFactory.Character();
        character.Exp = 0;
        var sut = CreateSut();

        var level = sut.LevelUp(character, _ecf);

        level.Should().Be(0);
        character.StatPoints.Should().Be(0);
        character.SkillPoints.Should().Be(0);
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