using System.Collections.Concurrent;
using Acorn.Data;
using Acorn.Database.Repository;
using Acorn.Game.Mappers;
using Acorn.Game.Models;
using Acorn.Game.Services;
using Acorn.Infrastructure.Communicators;
using Acorn.Infrastructure.Telemetry;
using Acorn.Net;
using Acorn.Net.PacketHandlers;
using Acorn.Net.PacketHandlers.Item;
using Acorn.Options;
using Acorn.Shared.Caching;
using Acorn.World;
using Acorn.World.Map;
using Acorn.World.Services.Map;
using Acorn.World.Services.Npc;
using Acorn.World.Services.Player;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Packet;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using NSubstitute;
using System.Threading.Tasks;

namespace Acorn.Tests.Net.PacketHandlers.Item;

public class ItemUseClientPacketHandlerTests
{
    private const int BeerItemId = 11;
    private const int EffectPotionItemId = 12;
    private const int CureCurseItemId = 13;
    private const int TeleportItemId = 14;
    private const int CursedHatId = 20;
    private const int NormalHatId = 21;

    private sealed class Harness
    {
        public required ItemUseClientPacketHandler Handler { get; init; }
        public required PlayerState Player { get; init; }
        public required Character Character { get; init; }
        public required MapState Map { get; init; }
        public required IPlayerController PlayerController { get; init; }
        public required IStatCalculator StatCalculator { get; init; }
        public required IMapEffectService MapEffectService { get; init; }
        public required List<byte[]> SentBytes { get; init; }
    }

    private static Eif CreateEif(Dictionary<int, EifRecord> items)
    {
        var maxId = items.Count == 0 ? 0 : items.Keys.Max();
        var list = new List<EifRecord>();
        for (var id = 1; id <= maxId; id++)
        {
            list.Add(items.TryGetValue(id, out var item)
                ? item
                : new EifRecord { Name = $"Item{id}" });
        }

        return new Eif { Items = list };
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
            MaxWeight = 100,
            Inventory = new Inventory(new ConcurrentBag<ItemWithAmount>()),
            Bank = new Bank(new ConcurrentBag<ItemWithAmount>()),
            Paperdoll = new Paperdoll(),
            Spells = new Spells([])
        };
    }

    private static MapState CreateMap(IDataFileRepository repository, bool canScroll = true)
    {
        var emf = new Emf
        {
            Width = 20,
            Height = 20,
            CanScroll = canScroll,
            Npcs = new List<MapNpc>(),
            TileSpecRows = new List<MapTileSpecRow>()
        };

        return new MapState(
            new MapWithId(1, emf),
            repository,
            Substitute.For<IMapBroadcastService>(),
            Substitute.For<IMapController>(),
            Substitute.For<INpcController>(),
            Substitute.For<IMapTileService>(),
            Substitute.For<IPaperdollService>(),
            playerRecoverRate: 90,
            isArenaEnabled: false,
            arenaSpawnInterval: 30,
            NullLogger<MapState>.Instance);
    }

    private static Harness CreateHarness(Eif eif, bool canScroll = true, Character? character = null)
    {
        character ??= CreateCharacter();

        var repository = Substitute.For<IDataFileRepository>();
        repository.Eif.Returns(eif);

        var map = CreateMap(repository, canScroll);

        var worldQueries = Substitute.For<IWorldQueries>();
        worldQueries.DataRepository.Returns(repository);
        worldQueries.FindMap(Arg.Any<int>()).Returns(map);

        var playerController = Substitute.For<IPlayerController>();
        var statCalculator = Substitute.For<IStatCalculator>();
        var mapEffectService = Substitute.For<IMapEffectService>();
        var characterRepository = Substitute.For<IDbRepository<Acorn.Database.Models.Character>>();
        characterRepository.UpdateAsync(Arg.Any<Acorn.Database.Models.Character>()).Returns(Task.CompletedTask);

        var handler = new ItemUseClientPacketHandler(
            NullLogger<ItemUseClientPacketHandler>.Instance,
            worldQueries,
            new InventoryService(new WeightCalculator(), repository),
            new WeightCalculator(),
            Substitute.For<IFormulaService>(),
            playerController,
            Substitute.For<IInnDataRepository>(),
            Substitute.For<ICharacterCacheService>(),
            Substitute.For<IPaperdollService>(),
            Substitute.For<ICharacterMapper>(),
            statCalculator,
            mapEffectService,
            characterRepository);

        var sentBytes = new List<byte[]>();
        var communicator = Substitute.For<ICommunicator>();
        communicator.IsConnected.Returns(false);
        communicator.Send(Arg.Any<IEnumerable<byte>>())
            .Returns(Task.CompletedTask)
            .AndDoes(call => sentBytes.Add(call.Arg<IEnumerable<byte>>().ToArray()));

        var player = new PlayerState(
            Array.Empty<IPacketHandler>(),
            communicator,
            NullLogger<PlayerState>.Instance,
            Microsoft.Extensions.Options.Options.Create(new ServerOptions
            {
                NewCharacter = new NewCharacterOptions { X = 6, Y = 6, Map = 1 },
                Hosting = new HostingOptions
                {
                    SLN = new SLNOptions
                    {
                        Enabled = false,
                        Url = "http://localhost",
                        PingRate = 5,
                        UserAgent = "Test",
                        Zone = "Test",
                        ServerName = "Test",
                        Site = "http://localhost"
                    },
                    HostName = "localhost",
                    Port = 1234,
                    WebSocketPort = 1235
                },
                TickRate = 1000
            }),
            new AcornMetrics(),
            sessionId: 1,
            onDispose: _ => Task.CompletedTask)
        {
            Character = character,
            CurrentMap = map
        };

        return new Harness
        {
            Handler = handler,
            Player = player,
            Character = character,
            Map = map,
            PlayerController = playerController,
            StatCalculator = statCalculator,
            MapEffectService = mapEffectService,
            SentBytes = sentBytes
        };
    }

    private static IPacket DecodeLastSentPacket(Harness harness)
    {
        harness.SentBytes.Should().NotBeEmpty("the handler should have sent a reply");
        var frame = harness.SentBytes[^1];

        // frame = [2-byte encoded length][action][family][encrypted payload]
        var body = frame.Skip(2).ToArray();
        var decrypted = DataEncrypter.SwapMultiples(
            DataEncrypter.Deinterleave(DataEncrypter.FlipMSB(body)),
            harness.Player.ServerEncryptionMulti);

        var reader = new EoReader(decrypted);
        var action = (PacketAction)reader.GetByte();
        var family = (PacketFamily)reader.GetByte();

        var resolver = new PacketResolver("Moffat.EndlessOnline.SDK.Protocol.Net.Server");
        var packet = resolver.Create(family, action);
        packet.Deserialize(reader.Slice());
        return packet;
    }

    private static void GiveItem(Character character, int itemId, int amount = 1)
    {
        character.Inventory.Items.Add(new ItemWithAmount { Id = itemId, Amount = amount });
    }

    [Test]
    public async Task Beer_ShouldConsumeItemAndReplyWithAlcoholType()
    {
        // Arrange
        var eif = CreateEif(new Dictionary<int, EifRecord>
        {
            [BeerItemId] = new() { Name = "Beer", Type = ItemType.Alcohol }
        });
        var harness = CreateHarness(eif);
        GiveItem(harness.Character, BeerItemId);

        // Act
        await harness.Handler.HandleAsync(harness.Player, new ItemUseClientPacket { ItemId = BeerItemId });

        // Assert
        harness.Character.Inventory.Items.Should().BeEmpty("beer is consumed on use");
        DecodeLastSentPacket(harness).Should().BeOfType<ItemReplyServerPacket>()
            .Which.ItemType.Should().Be(ItemType.Alcohol);
    }

    [Test]
    public async Task EffectPotion_ShouldConsumeItemBroadcastEffectAndReply()
    {
        // Arrange
        var eif = CreateEif(new Dictionary<int, EifRecord>
        {
            [EffectPotionItemId] = new() { Name = "Effect Potion", Type = ItemType.EffectPotion, Spec1 = 5 }
        });
        var harness = CreateHarness(eif);
        GiveItem(harness.Character, EffectPotionItemId);

        // Act
        await harness.Handler.HandleAsync(harness.Player, new ItemUseClientPacket { ItemId = EffectPotionItemId });

        // Assert
        harness.Character.Inventory.Items.Should().BeEmpty("the potion is consumed on use");

        var reply = DecodeLastSentPacket(harness).Should().BeOfType<ItemReplyServerPacket>().Subject;
        reply.ItemType.Should().Be(ItemType.EffectPotion);
        reply.ItemTypeData.Should().BeOfType<ItemReplyServerPacket.ItemTypeDataEffectPotion>()
            .Which.EffectId.Should().Be(5);

        await harness.MapEffectService.Received(1).EffectOnPlayersAsync(
            harness.Map,
            Arg.Is<IReadOnlyList<int>>(ids => ids.Contains(harness.Player.SessionId)),
            5,
            harness.Player.SessionId);
    }

    [Test]
    public async Task CureCurse_WhenCursedGearEquipped_ShouldDestroyGearConsumeItemAndReply()
    {
        // Arrange
        var eif = CreateEif(new Dictionary<int, EifRecord>
        {
            [CureCurseItemId] = new() { Name = "Cure Curse", Type = ItemType.CureCurse },
            [CursedHatId] = new() { Name = "Cursed Hat", Special = ItemSpecial.Cursed }
        });
        var harness = CreateHarness(eif);
        GiveItem(harness.Character, CureCurseItemId);
        harness.Character.Paperdoll.Hat = CursedHatId;

        // Act
        await harness.Handler.HandleAsync(harness.Player, new ItemUseClientPacket { ItemId = CureCurseItemId });

        // Assert
        harness.Character.Inventory.Items.Should().BeEmpty("the cure item is consumed");
        harness.Character.Paperdoll.Hat.Should().Be(0, "cursed equipment is destroyed");
        harness.StatCalculator.Received(1).RecalculateStats(harness.Character, Arg.Any<Ecf>());

        var reply = DecodeLastSentPacket(harness).Should().BeOfType<ItemReplyServerPacket>().Subject;
        reply.ItemType.Should().Be(ItemType.CureCurse);
        reply.ItemTypeData.Should().BeOfType<ItemReplyServerPacket.ItemTypeDataCureCurse>();
    }

    [Test]
    public async Task CureCurse_WhenNoCursedGear_ShouldNotConsumeItem()
    {
        // Arrange
        var eif = CreateEif(new Dictionary<int, EifRecord>
        {
            [CureCurseItemId] = new() { Name = "Cure Curse", Type = ItemType.CureCurse },
            [NormalHatId] = new() { Name = "Normal Hat", Special = ItemSpecial.Normal }
        });
        var harness = CreateHarness(eif);
        GiveItem(harness.Character, CureCurseItemId);
        harness.Character.Paperdoll.Hat = NormalHatId;

        // Act
        await harness.Handler.HandleAsync(harness.Player, new ItemUseClientPacket { ItemId = CureCurseItemId });

        // Assert
        harness.Character.Inventory.Items.Should().ContainSingle(i => i.Id == CureCurseItemId,
            "nothing was cured so the item must not be consumed");
        harness.Character.Paperdoll.Hat.Should().Be(NormalHatId);
        harness.SentBytes.Should().BeEmpty();
        harness.StatCalculator.DidNotReceiveWithAnyArgs().RecalculateStats(default!, default!);
    }

    [Test]
    public async Task Teleport_WhenAlreadyAtDestination_ShouldNotConsumeItem()
    {
        // Arrange
        var eif = CreateEif(new Dictionary<int, EifRecord>
        {
            [TeleportItemId] = new()
            {
                Name = "Scroll",
                Type = ItemType.Teleport,
                Spec1 = 1,
                Spec2 = 5,
                Spec3 = 5
            }
        });
        var harness = CreateHarness(eif, canScroll: true);
        GiveItem(harness.Character, TeleportItemId);
        harness.Character.Map = 1;
        harness.Character.X = 5;
        harness.Character.Y = 5;

        // Act
        await harness.Handler.HandleAsync(harness.Player, new ItemUseClientPacket { ItemId = TeleportItemId });

        // Assert
        harness.Character.Inventory.Items.Should().ContainSingle(i => i.Id == TeleportItemId);
        await harness.PlayerController.DidNotReceiveWithAnyArgs()
            .WarpAsync(default!, default!, default, default, default);
    }

    [Test]
    public async Task Teleport_WhenMapDisallowsScroll_ShouldNotConsumeItem()
    {
        // Arrange
        var eif = CreateEif(new Dictionary<int, EifRecord>
        {
            [TeleportItemId] = new()
            {
                Name = "Scroll",
                Type = ItemType.Teleport,
                Spec1 = 1,
                Spec2 = 5,
                Spec3 = 5
            }
        });
        var harness = CreateHarness(eif, canScroll: false);
        GiveItem(harness.Character, TeleportItemId);

        // Act
        await harness.Handler.HandleAsync(harness.Player, new ItemUseClientPacket { ItemId = TeleportItemId });

        // Assert
        harness.Character.Inventory.Items.Should().ContainSingle(i => i.Id == TeleportItemId);
        await harness.PlayerController.DidNotReceiveWithAnyArgs()
            .WarpAsync(default!, default!, default, default, default);
    }

    [Test]
    public async Task Teleport_WhenValid_ShouldConsumeItemAndWarp()
    {
        // Arrange
        var eif = CreateEif(new Dictionary<int, EifRecord>
        {
            [TeleportItemId] = new()
            {
                Name = "Scroll",
                Type = ItemType.Teleport,
                Spec1 = 1,
                Spec2 = 5,
                Spec3 = 5
            }
        });
        var harness = CreateHarness(eif, canScroll: true);
        GiveItem(harness.Character, TeleportItemId);

        // Act
        await harness.Handler.HandleAsync(harness.Player, new ItemUseClientPacket { ItemId = TeleportItemId });

        // Assert
        harness.Character.Inventory.Items.Should().BeEmpty("the scroll is consumed");
        await harness.PlayerController.Received(1).WarpAsync(
            harness.Player,
            harness.Map,
            5,
            5,
            WarpEffect.Scroll);
    }
}