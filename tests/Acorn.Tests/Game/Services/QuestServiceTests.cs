using System.Collections.Concurrent;
using Acorn.Data;
using Acorn.Database.Repository;
using Acorn.Game.Models;
using Acorn.Game.Services;
using Acorn.Infrastructure.Communicators;
using Acorn.Infrastructure.Telemetry;
using Acorn.Net;
using Acorn.Net.PacketHandlers;
using Acorn.Options;
using Acorn.Shared.Caching;
using Acorn.World;
using Acorn.World.Services.Player;
using Acorn.World.Services.Quest;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using NSubstitute;
using Xunit;

namespace Acorn.Tests.Game.Services;

public class QuestServiceTests
{
    private readonly IQuestDataRepository _questRepository = Substitute.For<IQuestDataRepository>();
    private readonly IInventoryService _inventoryService = new InventoryService();
    private readonly IFormulaService _formulaService = Substitute.For<IFormulaService>();
    private readonly IStatCalculator _statCalculator = Substitute.For<IStatCalculator>();
    private readonly IWeightCalculator _weightCalculator = Substitute.For<IWeightCalculator>();
    private readonly IDataFileRepository _dataFiles = Substitute.For<IDataFileRepository>();
    private readonly ICharacterCacheService _characterCache = Substitute.For<ICharacterCacheService>();
    private readonly IPaperdollService _paperdollService = Substitute.For<IPaperdollService>();
    private readonly IPlayerController _playerController = Substitute.For<IPlayerController>();
    private readonly IWorldQueries _worldQueries = Substitute.For<IWorldQueries>();
    private readonly IServiceScopeFactory _scopeFactory = Substitute.For<IServiceScopeFactory>();
    private readonly AcornMetrics _metrics = new();

    private QuestService CreateService()
    {
        return new QuestService(
            _questRepository,
            _inventoryService,
            _formulaService,
            _statCalculator,
            _weightCalculator,
            _dataFiles,
            _characterCache,
            _paperdollService,
            _playerController,
            _worldQueries,
            _scopeFactory,
            _metrics,
            NullLogger<QuestService>.Instance);
    }

    private void RegisterQuest(QuestData quest)
    {
        _questRepository.GetQuest(quest.Id).Returns(quest);
        _questRepository.Quests.Returns(new Dictionary<int, QuestData> { [quest.Id] = quest });
    }

    private static Character CreateCharacter()
    {
        return new Character
        {
            Accounts_Username = "testuser",
            Name = "TestCharacter",
            Map = 1,
            X = 1,
            Y = 1,
            Level = 1,
            Inventory = new Inventory(new ConcurrentBag<ItemWithAmount>()),
            Bank = new Bank(new ConcurrentBag<ItemWithAmount>()),
            Paperdoll = new Paperdoll(),
            Spells = new Spells(new ConcurrentBag<Spell>())
        };
    }

    private static PlayerState CreatePlayer(Character character)
    {
        var communicator = Substitute.For<ICommunicator>();
        communicator.IsConnected.Returns(false);

        var options = Microsoft.Extensions.Options.Options.Create(new ServerOptions
        {
            NewCharacter = new NewCharacterOptions { X = 1, Y = 1, Map = 1 },
            Hosting = new HostingOptions
            {
                SLN = new SLNOptions
                {
                    Enabled = false,
                    Url = "",
                    PingRate = 1,
                    UserAgent = "",
                    Zone = "",
                    ServerName = "",
                    Site = ""
                },
                HostName = "localhost",
                Port = 1,
                WebSocketPort = 2
            },
            TickRate = 100
        });

        return new PlayerState(
            Array.Empty<IPacketHandler>(),
            communicator,
            NullLogger<PlayerState>.Instance,
            options,
            new AcornMetrics(),
            sessionId: 1,
            _ => Task.CompletedTask)
        {
            Character = character
        };
    }

    private static QuestAction Action(string name, params QuestArg[] args)
    {
        return new QuestAction(name, args.ToList());
    }

    private static QuestRule Rule(string name, string @goto, params QuestArg[] args)
    {
        return new QuestRule(name, args.ToList(), @goto);
    }

    private static QuestArg I(int value)
    {
        return new QuestArg.IntArg(value);
    }

    [Fact]
    public async Task NotifyNpcKilled_WhenThresholdNotReached_ShouldIncrementWithoutAdvancing()
    {
        var quest = new QuestData(1, "Slay", 1,
        [
            new QuestState("begin", "", [],
                [Rule("KilledNpcs", "done", I(5), I(2))]),
            new QuestState("done", "", [Action("End")], [])
        ]);
        RegisterQuest(quest);

        var character = CreateCharacter();
        var progress = new CharacterQuestProgress { QuestId = 1, State = 0 };
        character.Quests.Add(progress);
        var player = CreatePlayer(character);

        await CreateService().NotifyNpcKilled(player, 5);

        progress.State.Should().Be(0);
        progress.GetNpcKills(5).Should().Be(1);
    }

    [Fact]
    public async Task NotifyNpcKilled_WhenThresholdReached_ShouldAdvanceAndResetCounter()
    {
        var quest = new QuestData(1, "Slay", 1,
        [
            new QuestState("begin", "", [],
                [Rule("KilledNpcs", "done", I(5), I(2))]),
            new QuestState("done", "", [Action("End")], [])
        ]);
        RegisterQuest(quest);

        var character = CreateCharacter();
        var progress = new CharacterQuestProgress { QuestId = 1, State = 0 };
        character.Quests.Add(progress);
        var player = CreatePlayer(character);

        var sut = CreateService();
        await sut.NotifyNpcKilled(player, 5);
        await sut.NotifyNpcKilled(player, 5);

        progress.State.Should().Be(1);
        progress.GetNpcKills(5).Should().Be(0);
        progress.DoneAt.Should().NotBeNull();
    }

    [Fact]
    public async Task NotifyNpcKilled_WhenNpcDoesNotMatch_ShouldNotTrackKill()
    {
        var quest = new QuestData(1, "Slay", 1,
        [
            new QuestState("begin", "", [],
                [Rule("KilledNpcs", "done", I(5), I(1))]),
            new QuestState("done", "", [], [])
        ]);
        RegisterQuest(quest);

        var character = CreateCharacter();
        var progress = new CharacterQuestProgress { QuestId = 1, State = 0 };
        character.Quests.Add(progress);
        var player = CreatePlayer(character);

        await CreateService().NotifyNpcKilled(player, 99);

        progress.State.Should().Be(0);
        progress.GetNpcKills(99).Should().Be(0);
    }

    [Fact]
    public async Task CheckQuestRules_WhenGotItemsSatisfied_ShouldAdvanceAndRunActions()
    {
        var quest = new QuestData(1, "Fetch", 1,
        [
            new QuestState("begin", "", [],
                [Rule("GotItems", "reward", I(100), I(3))]),
            new QuestState("reward", "", [Action("GiveItem", I(200), I(2))], [])
        ]);
        RegisterQuest(quest);

        var character = CreateCharacter();
        character.Inventory.Items.Add(new ItemWithAmount { Id = 100, Amount = 3 });
        var progress = new CharacterQuestProgress { QuestId = 1, State = 0 };
        character.Quests.Add(progress);
        var player = CreatePlayer(character);

        await CreateService().CheckQuestRules(player);

        progress.State.Should().Be(1);
        character.Inventory.Items.Should().Contain(i => i.Id == 200 && i.Amount == 2);
    }

    [Fact]
    public async Task CheckQuestRules_WhenGotItemsNotSatisfied_ShouldNotAdvance()
    {
        var quest = new QuestData(1, "Fetch", 1,
        [
            new QuestState("begin", "", [],
                [Rule("GotItems", "reward", I(100), I(3))]),
            new QuestState("reward", "", [Action("GiveItem", I(200), I(2))], [])
        ]);
        RegisterQuest(quest);

        var character = CreateCharacter();
        character.Inventory.Items.Add(new ItemWithAmount { Id = 100, Amount = 1 });
        var progress = new CharacterQuestProgress { QuestId = 1, State = 0 };
        character.Quests.Add(progress);
        var player = CreatePlayer(character);

        await CreateService().CheckQuestRules(player);

        progress.State.Should().Be(0);
        character.Inventory.Items.Should().NotContain(i => i.Id == 200);
    }

    [Fact]
    public async Task CheckQuestRules_WhenRemoveItemAction_ShouldRemoveFromInventory()
    {
        var quest = new QuestData(1, "TurnIn", 1,
        [
            new QuestState("begin", "", [], [Rule("Always", "turnin")]),
            new QuestState("turnin", "", [Action("RemoveItem", I(100), I(2))], [])
        ]);
        RegisterQuest(quest);

        var character = CreateCharacter();
        character.Inventory.Items.Add(new ItemWithAmount { Id = 100, Amount = 5 });
        var progress = new CharacterQuestProgress { QuestId = 1, State = 0 };
        character.Quests.Add(progress);
        var player = CreatePlayer(character);

        await CreateService().CheckQuestRules(player);

        character.Inventory.Items.Should().ContainSingle(i => i.Id == 100)
            .Which.Amount.Should().Be(3);
    }

    [Fact]
    public async Task CheckQuestRules_WhenGiveKarmaAction_ShouldClampToMaximum()
    {
        var quest = new QuestData(1, "Karma", 1,
        [
            new QuestState("begin", "", [], [Rule("Always", "reward")]),
            new QuestState("reward", "", [Action("GiveKarma", I(5000))], [])
        ]);
        RegisterQuest(quest);

        var character = CreateCharacter();
        character.Karma = 100;
        var progress = new CharacterQuestProgress { QuestId = 1, State = 0 };
        character.Quests.Add(progress);
        var player = CreatePlayer(character);

        await CreateService().CheckQuestRules(player);

        character.Karma.Should().Be(2000);
    }

    [Fact]
    public async Task CheckQuestRules_WhenRemoveKarmaAction_ShouldClampToZero()
    {
        var quest = new QuestData(1, "Karma", 1,
        [
            new QuestState("begin", "", [], [Rule("Always", "reward")]),
            new QuestState("reward", "", [Action("RemoveKarma", I(5000))], [])
        ]);
        RegisterQuest(quest);

        var character = CreateCharacter();
        character.Karma = 10;
        var progress = new CharacterQuestProgress { QuestId = 1, State = 0 };
        character.Quests.Add(progress);
        var player = CreatePlayer(character);

        await CreateService().CheckQuestRules(player);

        character.Karma.Should().Be(0);
    }

    [Fact]
    public async Task CheckQuestRules_WhenSetClassAction_ShouldChangeClassAndRecalculateStats()
    {
        var quest = new QuestData(1, "Class", 1,
        [
            new QuestState("begin", "", [], [Rule("Always", "reward")]),
            new QuestState("reward", "", [Action("SetClass", I(3))], [])
        ]);
        RegisterQuest(quest);

        var character = CreateCharacter();
        character.Class = 1;
        var progress = new CharacterQuestProgress { QuestId = 1, State = 0 };
        character.Quests.Add(progress);
        var player = CreatePlayer(character);

        await CreateService().CheckQuestRules(player);

        character.Class.Should().Be(3);
        _statCalculator.Received(1).RecalculateStats(character, Arg.Any<Ecf>());
    }

    [Fact]
    public async Task CheckQuestRules_WhenGiveExpAction_ShouldAddExperience()
    {
        var quest = new QuestData(1, "Exp", 1,
        [
            new QuestState("begin", "", [], [Rule("Always", "reward")]),
            new QuestState("reward", "", [Action("GiveExp", I(500))], [])
        ]);
        RegisterQuest(quest);

        var character = CreateCharacter();
        character.Exp = 0;
        _formulaService.CanLevelUp(character).Returns(false);
        var progress = new CharacterQuestProgress { QuestId = 1, State = 0 };
        character.Quests.Add(progress);
        var player = CreatePlayer(character);

        await CreateService().CheckQuestRules(player);

        character.Exp.Should().Be(500);
    }

    [Fact]
    public async Task CheckQuestRules_WhenGiveExpCausesLevelUp_ShouldLevelUpCharacter()
    {
        var quest = new QuestData(1, "Exp", 1,
        [
            new QuestState("begin", "", [], [Rule("Always", "reward")]),
            new QuestState("reward", "", [Action("GiveExp", I(10000))], [])
        ]);
        RegisterQuest(quest);

        var character = CreateCharacter();
        character.Exp = 0;
        var canLevel = true;
        _formulaService.CanLevelUp(character).Returns(_ => canLevel);
        _formulaService.LevelUp(character, Arg.Any<Ecf>()).Returns(_ =>
        {
            character.Level++;
            canLevel = false;
            return character.Level;
        });

        var progress = new CharacterQuestProgress { QuestId = 1, State = 0 };
        character.Quests.Add(progress);
        var player = CreatePlayer(character);

        await CreateService().CheckQuestRules(player);

        character.Level.Should().Be(2);
    }

    [Fact]
    public async Task CheckQuestRules_WhenAlwaysRulePointsAtSelf_ShouldNotRecurseForever()
    {
        var quest = new QuestData(1, "Loop", 1,
        [
            new QuestState("begin", "", [], [Rule("Always", "begin")])
        ]);
        RegisterQuest(quest);

        var character = CreateCharacter();
        var progress = new CharacterQuestProgress { QuestId = 1, State = 0 };
        character.Quests.Add(progress);
        var player = CreatePlayer(character);

        var act = async () => await CreateService().CheckQuestRules(player);

        await act.Should().NotThrowAsync();
        progress.State.Should().Be(0);
    }
}
