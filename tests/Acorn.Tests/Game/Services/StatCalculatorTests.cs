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
            Sp = 10,
            MaxSp = 10,
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

        // SP formula: Level/4 + 50 + AdjAgi*2 + Class.Agi*Level/10 = 0 + 50 + 0 + 0
        character.MaxSp.Should().Be(50);
        character.Sp.Should().Be(10, "current SP is clamped down but never reduced from creation");
    }
}
