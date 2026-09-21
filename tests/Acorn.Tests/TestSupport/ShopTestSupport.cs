using Acorn.Data;
using Acorn.Database.Repository;
using Acorn.Game.Models;
using Acorn.Game.Services;
using Acorn.Net;
using Acorn.World.Map;
using Acorn.World.Npc;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Packet;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using NSubstitute;
using NpcType = Moffat.EndlessOnline.SDK.Protocol.Pub.NpcType;

namespace Acorn.Tests.TestSupport;

/// <summary>
///     Shared fixtures for shop packet-handler tests: a map holding a shop NPC at (10,10),
///     an in-memory EIF, and the player wiring the handlers expect.
/// </summary>
internal static class ShopTestSupport
{
    public const int GoldItemId = 1;
    public const int BehaviorId = 1;
    public const int NpcIndex = 5;

    /// <summary>
    ///     A ready-to-drive shop scenario: player standing at (playerX, playerY) next to
    ///     a shop NPC (behavior ID 1) on a map with a substitute data repository.
    /// </summary>
    internal sealed class Fixture
    {
        public required PlayerState Player { get; init; }
        public required Character Character { get; init; }
        public required CapturingCommunicator Communicator { get; init; }
        public required IShopDataRepository Shops { get; init; }
        public required IDataFileRepository DataFiles { get; init; }
        public required IInventoryService Inventory { get; init; }
        public required MapState Map { get; init; }
        public required NpcState ShopNpc { get; init; }
    }

    public static Eif BuildEif(params (int Id, int Weight)[] items)
    {
        var weights = items.ToDictionary(i => i.Id, i => i.Weight);
        var maxId = weights.Count == 0 ? 1 : Math.Max(1, weights.Keys.Max());

        var records = new List<EifRecord>();
        for (var id = 1; id <= maxId; id++)
        {
            records.Add(new EifRecord
            {
                Name = id == GoldItemId ? "Gold" : $"Item{id}",
                Weight = weights.TryGetValue(id, out var weight) ? weight : 0
            });
        }

        return new Eif { Items = records };
    }

    public static ShopData BuildShop(
        List<ShopTradeItem>? trades = null,
        List<ShopCraftItem>? crafts = null,
        int minLevel = 0,
        int maxLevel = 0,
        int classRequirement = 0,
        string name = "Test Shop")
    {
        return new ShopData(BehaviorId, name, minLevel, maxLevel, classRequirement, trades ?? [], crafts ?? []);
    }

    public static Fixture Create(
        ShopData? shop = null,
        (int Id, int Weight)[]? items = null,
        IInventoryService? inventoryOverride = null,
        int playerX = 11,
        int playerY = 10)
    {
        var eif = BuildEif(items ?? []);

        var dataFiles = Substitute.For<IDataFileRepository>();
        dataFiles.Eif.Returns(eif);

        var map = FakeMap.Create();
        var npc = new NpcState(new EnfRecord { Name = "Shop Bob", Type = NpcType.Shop, BehaviorId = BehaviorId })
        {
            X = 10,
            Y = 10,
            Index = NpcIndex
        };
        map.Npcs[NpcIndex] = npc;

        var (player, communicator) = FakePlayer.Create();
        player.CurrentMap = map;
        player.Character!.X = playerX;
        player.Character.Y = playerY;
        player.Character.MaxWeight = 100;
        player.Character.Level = 1;

        var inventory = inventoryOverride ?? new InventoryService(new WeightCalculator(), dataFiles);

        var shops = Substitute.For<IShopDataRepository>();
        if (shop != null)
        {
            shops.GetShopByBehaviorId(BehaviorId).Returns(shop);
        }

        return new Fixture
        {
            Player = player,
            Character = player.Character!,
            Communicator = communicator,
            Shops = shops,
            DataFiles = dataFiles,
            Inventory = inventory,
            Map = map,
            ShopNpc = npc
        };
    }

    public static void GiveItem(this Character character, int itemId, int amount = 1)
    {
        character.Inventory.Items.Add(new ItemWithAmount { Id = itemId, Amount = amount });
    }

    /// <summary>
    ///     Decodes the most recent frame the server sent to the test player back into a
    ///     packet, mirroring the ItemUse handler tests.
    /// </summary>
    public static IPacket DecodeLastSent(Fixture fixture)
    {
        fixture.Communicator.Sent.Should().NotBeEmpty("the handler should have sent a reply");
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
