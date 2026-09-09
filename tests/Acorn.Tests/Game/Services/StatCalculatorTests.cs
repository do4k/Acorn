using System.Collections.Concurrent;
using Acorn.Database.Repository;
using Acorn.Game.Models;
using Acorn.Game.Services;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using NSubstitute;
using Xunit;

namespace Acorn.Tests.Game.Services;

public class StatCalculatorTests
{
    private readonly StatCalculator _sut;
    private readonly Ecf _ecf;

    public StatCalculatorTests()
    {
        // First class in the dat001.ecf file is Peasant (1-indexed id = 1).
        _ecf = new Ecf
        {
            Classes = new List<EcfRecord>
            {
                new() { Name = "Peasant", StatGroup = 4, Str = 0, Intl = 0, Wis = 0, Agi = 0, Con = 0, Cha = 0 },
                new() { Name = "Warrior", StatGroup = 0, Str = 1, Intl = 0, Wis = 0, Agi = 0, Con = 1, Cha = 0 }
            }
        };

        var dataFileRepository = Substitute.For<IDataFileRepository>();
        dataFileRepository.Eif.Returns(new Eif());
        _sut = new StatCalculator(dataFileRepository);
    }

    /// <summary>
    ///     Mirrors the character created by CharacterCreateClientPacketHandler:
    ///     the default class (1 = Peasant) with the same starting HP/TP/SP values.
    /// </summary>
    private static Character CreateNewCharacter()
    {
        return new Character
        {
            Accounts_Username = "testuser",
            Name = "TestCharacter",
            Class = 1,
            Level = 0,
            Hp = 10,
            MaxHp = 10,
            Tp = 10,
            MaxTp = 10,
            Sp = 20,
            MaxSp = 20,
            Inventory = new Inventory(new ConcurrentBag<ItemWithAmount>()),
            Bank = new Bank(new ConcurrentBag<ItemWithAmount>()),
            Paperdoll = new Paperdoll(),
            Spells = new Spells(new ConcurrentBag<Spell>())
        };
    }

    [Fact]
    public void RecalculateStats_WhenNewCharacterHasValidDefaultClass_ShouldHaveStaminaToAttack()
    {
        var character = CreateNewCharacter();

        _sut.RecalculateStats(character, _ecf);

        character.MaxSp.Should().BeGreaterThan(0, "a new character must have stamina available to attack");
        character.Sp.Should().BeGreaterThan(0, "a new character must have stamina available to attack");
        character.Sp.Should().BeLessThanOrEqualTo(character.MaxSp);
    }

    [Fact]
    public void RecalculateStats_WhenPeasantAtLevelZero_ShouldComputeExpectedMaxSp()
    {
        var character = CreateNewCharacter();

        _sut.RecalculateStats(character, _ecf);

        // SP formula (reoserv default): 20.0 + 2.0 * level = 20 + 0 = 20
        character.MaxSp.Should().Be(20);
        character.Sp.Should().Be(20, "a new character spawns with full stamina");
    }

    [Fact]
    public void RecalculateStats_WhenPeasantAtLevelZero_ShouldComputeExpectedMaxHpAndMaxTp()
    {
        var character = CreateNewCharacter();

        _sut.RecalculateStats(character, _ecf);

        // HP: 10.0 + 2.5*0 + 2.5*0 = 10, TP: 10.0 + 2.5*0 + 2.5*0 + 1.5*0 = 10
        character.MaxHp.Should().Be(10);
        character.MaxTp.Should().Be(10);
        character.MaxWeight.Should().Be(70);
    }

    [Fact]
    public void RecalculateStats_WhenLevelIncreases_ShouldScaleMaxSpAndMaxHp()
    {
        var character = CreateNewCharacter();
        character.Level = 5;

        _sut.RecalculateStats(character, _ecf);

        // SP: 20 + 2*5 = 30, HP: floor(10 + 2.5*5 + 0) = floor(22.5) = 22
        character.MaxSp.Should().Be(30);
        character.MaxHp.Should().Be(22);
        character.MaxTp.Should().Be(22);
    }

    [Fact]
    public void RecalculateStats_WhenWearingEquipment_ShouldIncreaseMaxHpButNotMaxSp()
    {
        // Arrange - an item with HP/TP/Str bonuses, equipped in the Armor slot
        var dataFileRepository = Substitute.For<IDataFileRepository>();
        dataFileRepository.Eif.Returns(new Eif
        {
            Items = new List<EifRecord> { new() { Name = "TestArmor", Hp = 50, Tp = 10, Str = 5, Agi = 3 } }
        });
        var sut = new StatCalculator(dataFileRepository);

        var character = CreateNewCharacter();
        character.Paperdoll.Armor = 1; // item id = index + 1

        // Act
        sut.RecalculateStats(character, _ecf);

        // Assert - equipment HP is added to MaxHp, but SP (stamina) is level-only
        character.MaxHp.Should().Be(50 + 10);
        character.MaxSp.Should().Be(20);
    }
}
