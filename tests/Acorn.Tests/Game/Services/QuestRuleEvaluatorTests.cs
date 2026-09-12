using System.Collections.Concurrent;
using Acorn.Data;
using Acorn.Game.Models;
using Acorn.World.Services.Quest;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol;

namespace Acorn.Tests.Game.Services;

public class QuestRuleEvaluatorTests
{
    private static Character CreateCharacter()
    {
        return new Character
        {
            Accounts_Username = "testuser",
            Name = "TestCharacter",
            Map = 5,
            X = 10,
            Y = 20,
            Class = 2,
            Race = 1,
            Gender = Gender.Male,
            Inventory = new Inventory(new ConcurrentBag<ItemWithAmount>()),
            Bank = new Bank(new ConcurrentBag<ItemWithAmount>()),
            Paperdoll = new Paperdoll(),
            Spells = new Spells(new ConcurrentBag<Spell>())
        };
    }

    private static QuestRule Rule(string name, string @goto, params QuestArg[] args)
    {
        return new QuestRule(name, args.ToList(), @goto);
    }

    [Test]
    public void Evaluate_Always_ShouldBeTrue()
    {
        var result = QuestRuleEvaluator.Evaluate(Rule("Always", "next"), CreateCharacter(),
            new CharacterQuestProgress());

        result.Should().BeTrue();
    }

    [Test]
    public void Evaluate_UnknownRule_ShouldBeFalse()
    {
        var result = QuestRuleEvaluator.Evaluate(Rule("SomeFutureRule", "next"), CreateCharacter(),
            new CharacterQuestProgress());

        result.Should().BeFalse();
    }

    [Test]
    public void Evaluate_GotItems_WhenPlayerHasEnough_ShouldBeTrue()
    {
        var character = CreateCharacter();
        character.Inventory.Items.Add(new ItemWithAmount { Id = 100, Amount = 3 });

        var result = QuestRuleEvaluator.Evaluate(
            Rule("GotItems", "next", new QuestArg.IntArg(100), new QuestArg.IntArg(3)), character,
            new CharacterQuestProgress());

        result.Should().BeTrue();
    }

    [Test]
    public void Evaluate_GotItems_WhenPlayerHasTooFew_ShouldBeFalse()
    {
        var character = CreateCharacter();
        character.Inventory.Items.Add(new ItemWithAmount { Id = 100, Amount = 2 });

        var result = QuestRuleEvaluator.Evaluate(
            Rule("GotItems", "next", new QuestArg.IntArg(100), new QuestArg.IntArg(3)), character,
            new CharacterQuestProgress());

        result.Should().BeFalse();
    }

    [Test]
    public void Evaluate_LostItems_WhenPlayerHasTooFew_ShouldBeTrue()
    {
        var character = CreateCharacter();

        var result = QuestRuleEvaluator.Evaluate(
            Rule("LostItems", "next", new QuestArg.IntArg(100), new QuestArg.IntArg(1)), character,
            new CharacterQuestProgress());

        result.Should().BeTrue();
    }

    [Test]
    public void Evaluate_KilledNpcs_WhenThresholdReached_ShouldBeTrue()
    {
        var progress = new CharacterQuestProgress();
        progress.AddNpcKill(7);
        progress.AddNpcKill(7);

        var result = QuestRuleEvaluator.Evaluate(
            Rule("KilledNpcs", "next", new QuestArg.IntArg(7), new QuestArg.IntArg(2)), CreateCharacter(),
            progress);

        result.Should().BeTrue();
    }

    [Test]
    public void Evaluate_KilledNpcs_WhenDifferentNpc_ShouldBeFalse()
    {
        var progress = new CharacterQuestProgress();
        progress.AddNpcKill(8);

        var result = QuestRuleEvaluator.Evaluate(
            Rule("KilledNpcs", "next", new QuestArg.IntArg(7), new QuestArg.IntArg(1)), CreateCharacter(),
            progress);

        result.Should().BeFalse();
    }

    [Test]
    public void Evaluate_KilledPlayers_WhenThresholdReached_ShouldBeTrue()
    {
        var progress = new CharacterQuestProgress { PlayerKills = 4 };

        var result = QuestRuleEvaluator.Evaluate(
            Rule("KilledPlayers", "next", new QuestArg.IntArg(3)), CreateCharacter(), progress);

        result.Should().BeTrue();
    }

    [Test]
    public void Evaluate_EnterMap_WhenOnMap_ShouldBeTrue()
    {
        var result = QuestRuleEvaluator.Evaluate(
            Rule("EnterMap", "next", new QuestArg.IntArg(5)), CreateCharacter(), new CharacterQuestProgress());

        result.Should().BeTrue();
    }

    [Test]
    public void Evaluate_EnterCoord_WhenOnCoord_ShouldBeTrue()
    {
        var result = QuestRuleEvaluator.Evaluate(
            Rule("EnterCoord", "next", new QuestArg.IntArg(5), new QuestArg.IntArg(10), new QuestArg.IntArg(20)),
            CreateCharacter(), new CharacterQuestProgress());

        result.Should().BeTrue();
    }

    [Test]
    public void Evaluate_LeaveMap_WhenOnDifferentMap_ShouldBeTrue()
    {
        var result = QuestRuleEvaluator.Evaluate(
            Rule("LeaveMap", "next", new QuestArg.IntArg(99)), CreateCharacter(), new CharacterQuestProgress());

        result.Should().BeTrue();
    }

    [Test]
    public void Evaluate_IsClass_WhenMatching_ShouldBeTrue()
    {
        var result = QuestRuleEvaluator.Evaluate(
            Rule("IsClass", "next", new QuestArg.IntArg(2)), CreateCharacter(), new CharacterQuestProgress());

        result.Should().BeTrue();
    }

    [Test]
    public void Evaluate_IsGender_WhenMatching_ShouldBeTrue()
    {
        var result = QuestRuleEvaluator.Evaluate(
            Rule("IsGender", "next", new QuestArg.IntArg((int)Gender.Male)), CreateCharacter(),
            new CharacterQuestProgress());

        result.Should().BeTrue();
    }

    [Test]
    public void Evaluate_IsRace_WhenMatching_ShouldBeTrue()
    {
        var result = QuestRuleEvaluator.Evaluate(
            Rule("IsRace", "next", new QuestArg.IntArg(1)), CreateCharacter(), new CharacterQuestProgress());

        result.Should().BeTrue();
    }
}