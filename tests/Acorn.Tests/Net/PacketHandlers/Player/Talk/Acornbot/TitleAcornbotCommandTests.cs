using Acorn.Database.Repository;
using Acorn.Game.Mappers;
using Acorn.Game.Models;
using Acorn.Game.Services;
using Acorn.Net;
using Acorn.Net.PacketHandlers.Player.Talk.Acornbot;
using Acorn.Options;
using Acorn.Shared.Caching;
using Acorn.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Packet;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using NSubstitute;
using OptionsFactory = Microsoft.Extensions.Options.Options;
using DatabaseCharacter = Acorn.Database.Models.Character;

namespace Acorn.Tests.Net.PacketHandlers.Player.Talk.Acornbot;

/// <summary>
///     The Acornbot title command applies titles for free by default and can charge
///     an item ("title certificate" style id) or gold (id 1) when configured.
/// </summary>
public class TitleAcornbotCommandTests
{
    private const int CertItemId = 2; // second record of the fixture's tiny EIF
    private const int GoldItemId = 1;

    private sealed record Fixture(
        TitleAcornbotCommand Sut,
        PlayerState Player,
        CapturingCommunicator Communicator,
        IAcornbotReplyChannel Replies,
        IDbRepository<DatabaseCharacter> Repository);

    private static Fixture CreateSut(AcornbotTitleOptions? titleOptions = null, IBannedTextPolicy? bannedText = null)
    {
        var eif = new Eif
        {
            Items =
            [
                new EifRecord { Name = "Gold", Weight = 0 },
                new EifRecord { Name = "Title Certificate", Weight = 1 }
            ]
        };

        var dataFiles = Substitute.For<IDataFileRepository>();
        dataFiles.Eif.Returns(eif);

        var (player, communicator) = FakePlayer.Create("Tester");
        player.Character!.MaxWeight = 100;

        var replies = Substitute.For<IAcornbotReplyChannel>();
        var repository = Substitute.For<IDbRepository<DatabaseCharacter>>();

        var sut = new TitleAcornbotCommand(
            replies,
            OptionsFactory.Create(new AcornbotOptions
            {
                Enabled = true,
                Title = titleOptions ?? new AcornbotTitleOptions()
            }),
            bannedText ?? Substitute.For<IBannedTextPolicy>(),
            new InventoryService(new WeightCalculator(), dataFiles),
            new WeightCalculator(),
            dataFiles,
            Substitute.For<ICharacterCacheService>(),
            Substitute.For<IPaperdollService>(),
            repository,
            new CharacterMapper(),
            NullLogger<TitleAcornbotCommand>.Instance);

        return new Fixture(sut, player, communicator, replies, repository);
    }

    private static void Give(Fixture fixture, int itemId, int amount) =>
        fixture.Player.Character!.Inventory.Items.Add(
            new ItemWithAmount { Id = itemId, Amount = amount });

    [Test]
    public async Task Title_WhenFree_AppliesPersistsAndConfirms()
    {
        // Arrange
        var fixture = CreateSut();

        // Act
        await fixture.Sut.HandleAsync(fixture.Player, "title", "Cool", "Dude");

        // Assert
        fixture.Player.Character!.Title.Should().Be("Cool Dude");
        await fixture.Repository.Received(1)
            .UpdateAsync(Arg.Is<DatabaseCharacter>(c => c.Title == "Cool Dude"));
        await fixture.Replies.Received(1)
            .WhisperAsync(fixture.Player, Arg.Is<string>(m => m.Contains("\"Cool Dude\"")));
        fixture.Communicator.Sent.Should().BeEmpty("no cost was charged, no item sync is sent");
    }

    [Test]
    public async Task Title_WhenMapPresent_ReannouncesWithoutSendingAnythingToSelf()
    {
        // Arrange - NotifyAppear only targets nearby viewers, never the acting
        // player, and must not re-warp them.
        var fixture = CreateSut();
        var map = FakeMap.Create();
        fixture.Player.CurrentMap = map;

        // Act
        await fixture.Sut.HandleAsync(fixture.Player, "title", "Seen");

        // Assert
        fixture.Player.Character!.Title.Should().Be("Seen");
        fixture.Communicator.Sent.Should().BeEmpty("no warp or self-info packet is sent");
    }

    [Test]
    public async Task Title_WhenBannedSymbol_RejectedBeforeCharging()
    {
        // Arrange - '#' is banned and a cost is configured; rejection must be free.
        var banned = Substitute.For<IBannedTextPolicy>();
        banned.FirstViolation(Arg.Any<string?>())
            .Returns(ci => ((string?)ci.ArgAt<string?>(0))?.Contains('#') == true ? "#" : null);
        var fixture = CreateSut(new AcornbotTitleOptions
        {
            CostItemId = GoldItemId,
            CostAmount = 10_000
        }, banned);
        Give(fixture, GoldItemId, 25_000);

        // Act
        await fixture.Sut.HandleAsync(fixture.Player, "title", "#1", "Dad");

        // Assert
        fixture.Player.Character!.Title.Should().BeNull();
        fixture.Player.Character.Inventory.Items.Single(i => i.Id == GoldItemId).Amount.Should().Be(25_000);
        await fixture.Replies.Received(1).WhisperAsync(fixture.Player,
            Arg.Is<string>(m => m.Contains("\"#\"")));
        await fixture.Repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
    }

    [Test]
    public async Task Title_WhenTooLong_RejectedWithLimit()
    {
        var fixture = CreateSut(new AcornbotTitleOptions { MaxLength = 10 });

        await fixture.Sut.HandleAsync(fixture.Player, "title", "way", "too", "long", "for", "me");

        fixture.Player.Character!.Title.Should().BeNull();
        await fixture.Replies.Received(1).WhisperAsync(fixture.Player,
            Arg.Is<string>(m => m.Contains("at most 10 characters")));
        await fixture.Repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
    }

    [Test]
    public async Task Title_NoArguments_ReceivesUsage()
    {
        var fixture = CreateSut();

        await fixture.Sut.HandleAsync(fixture.Player, "title");

        await fixture.Replies.Received(1).WhisperAsync(fixture.Player,
            Arg.Is<string>(m => m.StartsWith("Usage:")));
    }

    [Test]
    public async Task Title_Clear_RemovesTitleWithoutCharging()
    {
        // Arrange - a gold cost is configured but clearing must stay free
        var fixture = CreateSut(new AcornbotTitleOptions
        {
            CostItemId = GoldItemId,
            CostAmount = 10_000
        });
        fixture.Player.Character!.Title = "Old Title";

        // Act
        await fixture.Sut.HandleAsync(fixture.Player, "title", "clear");

        // Assert
        fixture.Player.Character.Title.Should().BeNull();
        await fixture.Replies.Received(1)
            .WhisperAsync(fixture.Player, Arg.Is<string>(m => m.Contains("cleared")));
        await fixture.Repository.Received(1).UpdateAsync(Arg.Is<DatabaseCharacter>(c => c.Title == null));
    }

    [Test]
    public async Task Title_WhenGoldCostMet_ChargesSyncsAndApplies()
    {
        // Arrange
        var fixture = CreateSut(new AcornbotTitleOptions
        {
            CostItemId = GoldItemId,
            CostAmount = 10_000
        });
        Give(fixture, GoldItemId, 25_000);

        // Act
        await fixture.Sut.HandleAsync(fixture.Player, "title", "Rich");

        // Assert
        fixture.Player.Character!.Title.Should().Be("Rich");
        fixture.Player.Character.Inventory.Items.Single(i => i.Id == GoldItemId).Amount.Should().Be(15_000);
        await fixture.Replies.Received(1).WhisperAsync(fixture.Player,
            Arg.Is<string>(m => m.Contains("Your title is now \"Rich\"") && m.Contains("Paid 10,000 gold")));

        var packet = DecodeLastSent(fixture);
        packet.Should().BeOfType<ItemReplyServerPacket>();
        var reply = (ItemReplyServerPacket)packet;
        reply.UsedItem.Id.Should().Be(GoldItemId);
        reply.UsedItem.Amount.Should().Be(15_000);
    }

    [Test]
    public async Task Title_WhenGoldCostNotMet_RefusedAndTitleUnchanged()
    {
        var fixture = CreateSut(new AcornbotTitleOptions
        {
            CostItemId = GoldItemId,
            CostAmount = 10_000
        });
        Give(fixture, GoldItemId, 500);

        await fixture.Sut.HandleAsync(fixture.Player, "title", "Broke");

        fixture.Player.Character!.Title.Should().BeNull();
        fixture.Player.Character.Inventory.Items.Single(i => i.Id == GoldItemId).Amount.Should().Be(500);
        await fixture.Replies.Received(1).WhisperAsync(fixture.Player,
            Arg.Is<string>(m => m.Contains("costs 10,000 gold") && m.Contains("you have 500 gold")));
        await fixture.Repository.DidNotReceiveWithAnyArgs().UpdateAsync(default!);
        fixture.Communicator.Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Title_WhenCertificateCostMet_ConsumesItemAndApplies()
    {
        // Arrange
        var fixture = CreateSut(new AcornbotTitleOptions
        {
            CostItemId = CertItemId,
            CostAmount = 2
        });
        Give(fixture, CertItemId, 3);

        // Act
        await fixture.Sut.HandleAsync(fixture.Player, "title", "Certified");

        // Assert
        fixture.Player.Character!.Title.Should().Be("Certified");
        fixture.Player.Character.Inventory.Items.Single(i => i.Id == CertItemId).Amount.Should().Be(1);
        await fixture.Replies.Received(1).WhisperAsync(fixture.Player,
            Arg.Is<string>(m => m.Contains("Paid 2x Title Certificate")));
    }

    [Test]
    public async Task Title_WhenCertificateMissing_RefusedWithItemName()
    {
        var fixture = CreateSut(new AcornbotTitleOptions
        {
            CostItemId = CertItemId,
            CostAmount = 1
        });

        await fixture.Sut.HandleAsync(fixture.Player, "title", "Wanted");

        fixture.Player.Character!.Title.Should().BeNull();
        await fixture.Replies.Received(1).WhisperAsync(fixture.Player,
            Arg.Is<string>(m =>
                m.Contains("costs 1x Title Certificate") && m.Contains("you have 0x Title Certificate")));
    }

    /// <summary>
    ///     Decodes the last frame sent to the fixture player back into a packet,
    ///     mirroring <see cref="ShopTestSupport" />.
    /// </summary>
    private static IPacket DecodeLastSent(Fixture fixture)
    {
        fixture.Communicator.Sent.Should().NotBeEmpty("the handler should have sent an inventory sync");
        var frame = fixture.Communicator.Sent[^1];

        // frame = [2-byte encoded length][action][family][encrypted payload]
        var body = frame.Skip(2).ToArray();
        var decrypted = DataEncrypter.SwapMultiples(
            DataEncrypter.Deinterleave(DataEncrypter.FlipMSB(body)),
            fixture.Player.ServerEncryptionMulti);

        var reader = new EoReader(decrypted);
        var action = (PacketAction)reader.GetByte();
        var family = (PacketFamily)reader.GetByte();

        var resolver = new PacketResolver("Moffat.EndlessOnline.SDK.Protocol.Net.Server");
        var packet = resolver.Create(family, action);
        packet.Deserialize(reader.Slice());
        return packet;
    }
}
