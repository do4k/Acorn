using Acorn.Database.Repository;
using Acorn.Game.Mappers;
using Acorn.Game.Models;
using Acorn.Game.Services;
using Acorn.Infrastructure.Telemetry;
using Acorn.Net;
using Acorn.Net.PacketHandlers.Item;
using Acorn.Options;
using Acorn.Tests.Support;
using Acorn.World.Services.Map;
using Acorn.World.Services.Quest;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using NSubstitute;
using DbCharacter = Acorn.Database.Models.Character;
using GameCharacter = Acorn.Game.Models.Character;
using System.Threading.Tasks;

namespace Acorn.Tests.Net.PacketHandlers;

public class ItemDropClientPacketHandlerTests
{
    private readonly ICharacterMapper _characterMapper = Substitute.For<ICharacterMapper>();
    private readonly IDbRepository<DbCharacter> _characterRepository = Substitute.For<IDbRepository<DbCharacter>>();
    private readonly IDataFileRepository _dataRepository = Substitute.For<IDataFileRepository>();
    private readonly Eif _eif = new() { Items = new List<EifRecord>() };
    private readonly IInventoryService _inventoryService = Substitute.For<IInventoryService>();
    private readonly IMapItemService _mapItemService = Substitute.For<IMapItemService>();
    private readonly IQuestService _questService = Substitute.For<IQuestService>();
    private readonly IWeightCalculator _weightCalculator = Substitute.For<IWeightCalculator>();

    public ItemDropClientPacketHandlerTests()
    {
        _dataRepository.Eif.Returns(_eif);
        _mapItemService.TryDropItem(Arg.Any<PlayerState>(), Arg.Any<Acorn.World.Map.MapState>(),
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<Coords>())
            .Returns(new ItemDropResult(true, 1));
        _characterMapper.ToDatabase(Arg.Any<GameCharacter>())
            .Returns(new DbCharacter { Accounts_Username = "test", Name = "Test" });
        _inventoryService.GetItemAmount(Arg.Any<GameCharacter>(), Arg.Any<int>()).Returns(0);
        _weightCalculator.GetCurrentWeight(Arg.Any<GameCharacter>(), Arg.Any<Eif>()).Returns(0);
    }

    private ItemDropClientPacketHandler CreateHandler(int maxDrop = 10000)
    {
        return new ItemDropClientPacketHandler(
            NullLogger<ItemDropClientPacketHandler>.Instance,
            _mapItemService,
            _characterMapper,
            _weightCalculator,
            _inventoryService,
            _dataRepository,
            _characterRepository,
            Microsoft.Extensions.Options.Options.Create(TestFactories.CreateServerOptions(maxDrop)),
            _questService,
            new AcornMetrics());
    }

    private void AddItem(int id, ItemSpecial special = ItemSpecial.Normal)
    {
        while (_eif.Items.Count < id)
        {
            _eif.Items.Add(new EifRecord { Name = $"Item{_eif.Items.Count + 1}", Special = ItemSpecial.Normal });
        }

        _eif.Items[id - 1] = new EifRecord { Name = $"Item{id}", Special = special };
    }

    private static ItemDropClientPacket CreatePacket(int itemId, int amount, int x, int y)
    {
        return new ItemDropClientPacket
        {
            Item = new ThreeItem { Id = itemId, Amount = amount },
            Coords = new ByteCoords { X = x, Y = y }
        };
    }

    private static (PlayerState Player, Acorn.World.Map.MapState Map) CreatePlayerOnMap(int x = 5, int y = 5)
    {
        var map = TestFactories.CreateMap();
        var player = TestFactories.CreatePlayer(1, x, y);
        player.CurrentMap = map;
        map.Players[player.SessionId] = player;
        return (player, map);
    }

    [Test]
    public async Task HandleAsync_WhenItemIsLore_DoesNotDrop()
    {
        AddItem(1, ItemSpecial.Lore);
        var (player, _) = CreatePlayerOnMap();

        await CreateHandler().HandleAsync(player, CreatePacket(itemId: 1, amount: 1, x: 6, y: 6));

        await _mapItemService.DidNotReceive().TryDropItem(
            Arg.Any<PlayerState>(), Arg.Any<Acorn.World.Map.MapState>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<Coords>());
    }

    [Test]
    public async Task HandleAsync_WhenItemIsUnknown_DoesNotDrop()
    {
        var (player, _) = CreatePlayerOnMap();

        await CreateHandler().HandleAsync(player, CreatePacket(itemId: 99, amount: 1, x: 6, y: 6));

        await _mapItemService.DidNotReceive().TryDropItem(
            Arg.Any<PlayerState>(), Arg.Any<Acorn.World.Map.MapState>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<Coords>());
    }

    [Test]
    public async Task HandleAsync_WhenPlayerIsJailed_DoesNotDrop()
    {
        AddItem(1);
        var (player, _) = CreatePlayerOnMap();
        player.IsJailed = true;

        await CreateHandler().HandleAsync(player, CreatePacket(itemId: 1, amount: 1, x: 6, y: 6));

        await _mapItemService.DidNotReceive().TryDropItem(
            Arg.Any<PlayerState>(), Arg.Any<Acorn.World.Map.MapState>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<Coords>());
    }

    [Test]
    public async Task HandleAsync_WhenAmountExceedsMaxDrop_ClampsAmount()
    {
        AddItem(1);
        var (player, map) = CreatePlayerOnMap();

        await CreateHandler(maxDrop: 10).HandleAsync(player, CreatePacket(itemId: 1, amount: 500, x: 6, y: 6));

        await _mapItemService.Received(1).TryDropItem(player, map, 1, 10, Arg.Any<Coords>());
    }

    [Test]
    public async Task HandleAsync_WhenSentinelCoords_ConvertsToPlayerCoords()
    {
        AddItem(1);
        var (player, map) = CreatePlayerOnMap(x: 12, y: 9);

        await CreateHandler().HandleAsync(player, CreatePacket(itemId: 1, amount: 1, x: 255, y: 255));

        await _mapItemService.Received(1).TryDropItem(
            player, map, 1, 1, Arg.Is<Coords>(c => c.X == 12 && c.Y == 9));
    }

    [Test]
    public async Task HandleAsync_WhenTargetCoordsProvided_AppliesByteOffset()
    {
        AddItem(1);
        var (player, map) = CreatePlayerOnMap();

        await CreateHandler().HandleAsync(player, CreatePacket(itemId: 1, amount: 1, x: 7, y: 8));

        await _mapItemService.Received(1).TryDropItem(
            player, map, 1, 1, Arg.Is<Coords>(c => c.X == 6 && c.Y == 7));
    }
}