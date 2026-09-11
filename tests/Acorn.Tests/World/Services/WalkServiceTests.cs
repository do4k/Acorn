using Acorn.Database.Repository;
using Acorn.Game.Models;
using Acorn.Game.Services;
using Acorn.Infrastructure.Communicators;
using Acorn.Infrastructure.Telemetry;
using Acorn.Net;
using Acorn.Net.Models;
using Acorn.Net.PacketHandlers;
using Acorn.Net.PacketHandlers.Player;
using Acorn.Options;
using Acorn.Shared.Caching;
using Acorn.World;
using Acorn.World.Map;
using Acorn.World.Services.Map;
using Acorn.World.Services.Player;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using NSubstitute;
using Xunit;

namespace Acorn.Tests.World.Services;

/// <summary>
///     Unit tests for the server-authoritative walk validation. These cover the
///     cases that are awkward to reach through the integration client: occupied
///     tiles, interaction cleanup, trade cancellation and timestamp enforcement.
/// </summary>
public class WalkServiceTests
{
    private const int SessionId = 1;
    private const int PartnerSessionId = 2;

    [Fact]
    public async Task WalkAsync_WhenDestinationIsUnwalkable_ShouldNotMoveAndShouldRefresh()
    {
        var map = CreateMap(withWallAt: new Coords { X = 7, Y = 6 });
        var (player, _) = CreatePlayer(map, SessionId, 6, 6);
        var playerController = Substitute.For<IPlayerController>();
        var service = CreateService(playerController);

        await service.WalkAsync(player, Direction.Right, timestamp: 0, new Coords { X = 7, Y = 6 });

        player.Character!.X.Should().Be(6, "a wall blocks the move");
        player.Character.Y.Should().Be(6);
        await playerController.Received(1).RefreshAsync(player);
    }

    [Fact]
    public async Task WalkAsync_WhenDestinationIsOutOfBounds_ShouldNotMoveAndShouldRefresh()
    {
        var map = CreateMap();
        var (player, _) = CreatePlayer(map, SessionId, 0, 6);
        var playerController = Substitute.For<IPlayerController>();
        var service = CreateService(playerController);

        await service.WalkAsync(player, Direction.Left, timestamp: 0, new Coords { X = -1, Y = 6 });

        player.Character!.X.Should().Be(0, "walking off the map is rejected");
        await playerController.Received(1).RefreshAsync(player);
    }

    [Fact]
    public async Task WalkAsync_WhenDestinationIsOccupiedByAnotherPlayer_ShouldNotMove()
    {
        var map = CreateMap();
        var (player, _) = CreatePlayer(map, SessionId, 6, 6);
        var (other, _) = CreatePlayer(map, PartnerSessionId, 7, 6);
        var playerController = Substitute.For<IPlayerController>();
        var service = CreateService(playerController);

        await service.WalkAsync(player, Direction.Right, timestamp: 0, new Coords { X = 7, Y = 6 });

        player.Character!.X.Should().Be(6, "another player occupies the destination");
        other.Character!.X.Should().Be(7);
        await playerController.Received(1).RefreshAsync(player);
    }

    [Fact]
    public async Task WalkAsync_WhenTimestampTooClose_ShouldIgnoreTheMove()
    {
        var map = CreateMap();
        var (player, _) = CreatePlayer(map, SessionId, 6, 6);
        player.Timestamp = 1000;
        var playerController = Substitute.For<IPlayerController>();
        var service = CreateService(playerController, enforceTimestamps: true);

        await service.WalkAsync(player, Direction.Right, timestamp: 1010, new Coords { X = 7, Y = 6 });

        player.Character!.X.Should().Be(6, "the packet arrived too soon after the previous one");
        player.Timestamp.Should().Be(1000, "the rejected timestamp must not be stored");
        await playerController.DidNotReceive().RefreshAsync(Arg.Any<PlayerState>());
    }

    [Fact]
    public async Task WalkAsync_WhenTimestampAdvancedEnough_ShouldMove()
    {
        var map = CreateMap();
        var (player, _) = CreatePlayer(map, SessionId, 6, 6);
        player.Timestamp = 1000;
        var service = CreateService(Substitute.For<IPlayerController>(), enforceTimestamps: true);

        await service.WalkAsync(player, Direction.Right, timestamp: 1040, new Coords { X = 7, Y = 6 });

        player.Character!.X.Should().Be(7);
        player.Timestamp.Should().Be(1040);
    }

    [Fact]
    public async Task WalkAsync_WhenValid_ShouldMoveAndClearInteractions()
    {
        var map = CreateMap();
        var (player, _) = CreatePlayer(map, SessionId, 6, 6);
        player.InteractingNpcIndex = 5;
        player.InteractingBoardId = 3;
        player.InteractingChestCoords = new Coords { X = 6, Y = 6 };
        var service = CreateService(Substitute.For<IPlayerController>());

        await service.WalkAsync(player, Direction.Right, timestamp: 0, new Coords { X = 7, Y = 6 });

        player.Character!.X.Should().Be(7);
        player.Character.Y.Should().Be(6);
        player.InteractingNpcIndex.Should().BeNull();
        player.InteractingBoardId.Should().BeNull();
        player.InteractingChestCoords.Should().BeNull();
    }

    [Fact]
    public async Task WalkAsync_WhenTrading_ShouldCancelTradeAndNotifyPartner()
    {
        var map = CreateMap();
        var (player, _) = CreatePlayer(map, SessionId, 6, 6);
        var (partner, partnerCommunicator) = CreatePlayer(map, PartnerSessionId, 8, 6);

        player.TradeSession = new TradeSession { PartnerId = partner.SessionId, Partner = partner };
        partner.TradeSession = new TradeSession { PartnerId = player.SessionId, Partner = player };

        var service = CreateService(Substitute.For<IPlayerController>());

        await service.WalkAsync(player, Direction.Right, timestamp: 0, new Coords { X = 7, Y = 6 });

        player.TradeSession.Should().BeNull();
        partner.TradeSession.Should().BeNull();
        await partnerCommunicator.Received(1).Send(Arg.Any<IEnumerable<byte>>());
    }

    [Fact]
    public async Task WalkAsync_WhenDesynced_ShouldStillMoveButRefreshTheClient()
    {
        var map = CreateMap();
        var (player, _) = CreatePlayer(map, SessionId, 6, 6);
        var playerController = Substitute.For<IPlayerController>();
        var service = CreateService(playerController);

        // The client claims it moved to (9, 9) but only asked to step right.
        await service.WalkAsync(player, Direction.Right, timestamp: 0, new Coords { X = 9, Y = 9 });

        player.Character!.X.Should().Be(7, "the server applies its own authoritative move");
        await playerController.Received(1).RefreshAsync(player);
    }

    [Fact]
    public async Task AdminWalk_WhenPlayerIsNotAdmin_ShouldBeIgnored()
    {
        var map = CreateMap();
        var (player, _) = CreatePlayer(map, SessionId, 6, 6);
        player.Character!.Admin = AdminLevel.Player;

        var walkService = Substitute.For<IWalkService>();
        var handler = new WalkAdminClientPacketHandler(walkService, NullLogger<WalkAdminClientPacketHandler>.Instance);

        await handler.HandleAsync(player, CreateAdminWalkPacket(Direction.Right, 7, 6));

        await walkService.DidNotReceive().WalkAsync(
            Arg.Any<PlayerState>(), Arg.Any<Direction>(), Arg.Any<int>(),
            Arg.Any<Coords>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task AdminWalk_WhenPlayerIsGuardian_ShouldDelegateAsAdmin()
    {
        var map = CreateMap();
        var (player, _) = CreatePlayer(map, SessionId, 6, 6);
        player.Character!.Admin = AdminLevel.Guardian;

        var walkService = Substitute.For<IWalkService>();
        var handler = new WalkAdminClientPacketHandler(walkService, NullLogger<WalkAdminClientPacketHandler>.Instance);

        await handler.HandleAsync(player, CreateAdminWalkPacket(Direction.Right, 7, 6));

        await walkService.Received(1).WalkAsync(
            player, Direction.Right, Arg.Any<int>(), Arg.Any<Coords>(), admin: true);
    }

    // --- Helpers ---

    private static WalkAdminClientPacket CreateAdminWalkPacket(Direction direction, int x, int y)
    {
        return new WalkAdminClientPacket
        {
            WalkAction = new Moffat.EndlessOnline.SDK.Protocol.Net.Client.WalkAction
            {
                Direction = direction,
                Timestamp = 0,
                Coords = new Coords { X = x, Y = y }
            }
        };
    }

    private static WalkService CreateService(IPlayerController playerController, bool enforceTimestamps = false)
    {
        var serverOptions = new ServerOptions
        {
            NewCharacter = new NewCharacterOptions { X = 6, Y = 6, Map = 1 },
            Hosting = CreateHostingOptions(),
            TickRate = 1000,
            EnforceTimestamps = enforceTimestamps
        };

        return new WalkService(
            NullLogger<WalkService>.Instance,
            Substitute.For<IWorldQueries>(),
            playerController,
            new MapTileService(),
            Substitute.For<ICharacterCacheService>(),
            Substitute.For<IPaperdollService>(),
            Microsoft.Extensions.Options.Options.Create(serverOptions));
    }

    private static HostingOptions CreateHostingOptions()
    {
        return new HostingOptions
        {
            SLN = new SLNOptions
            {
                Enabled = false,
                Url = "http://localhost",
                PingRate = 5,
                UserAgent = "test",
                Zone = "test",
                ServerName = "test",
                Site = "http://localhost"
            },
            HostName = "localhost",
            Port = 8078,
            WebSocketPort = 8079
        };
    }

    private static MapState CreateMap(Coords? withWallAt = null)
    {
        var dataRepository = Substitute.For<IDataFileRepository>();
        dataRepository.Enf.Returns(new Enf
        {
            Rid = new List<int> { 1, 2 },
            Version = 1,
            TotalNpcsCount = 0,
            Npcs = new List<EnfRecord>()
        });

        var tileSpecRows = new List<MapTileSpecRow>();
        if (withWallAt is { } wall)
        {
            tileSpecRows.Add(new MapTileSpecRow
            {
                Y = wall.Y,
                Tiles = new List<MapTileSpecRowTile>
                {
                    new() { X = wall.X, TileSpec = MapTileSpec.Wall }
                }
            });
        }

        var emf = new Emf
        {
            Width = 20,
            Height = 20,
            Npcs = new List<MapNpc>(),
            TileSpecRows = tileSpecRows
        };

        return new MapState(
            new MapWithId(1, emf),
            dataRepository,
            Substitute.For<IMapBroadcastService>(),
            Substitute.For<IMapController>(),
            Substitute.For<Acorn.World.Services.Npc.INpcController>(),
            Substitute.For<IPaperdollService>(),
            playerRecoverRate: 90,
            isArenaEnabled: false,
            arenaSpawnInterval: 30,
            NullLogger<MapState>.Instance);
    }

    private static (PlayerState Player, ICommunicator Communicator) CreatePlayer(MapState map, int sessionId, int x, int y)
    {
        var communicator = Substitute.For<ICommunicator>();
        communicator.IsConnected.Returns(false);

        var character = new Character
        {
            Accounts_Username = $"user{sessionId}",
            Name = $"Player{sessionId}",
            X = x,
            Y = y,
            SitState = SitState.Stand,
            Admin = AdminLevel.Player,
            Inventory = new Inventory([]),
            Bank = new Bank([]),
            Paperdoll = new Paperdoll(),
            Spells = new Spells([])
        };

        var serverOptions = new ServerOptions
        {
            NewCharacter = new NewCharacterOptions { X = 6, Y = 6, Map = 1 },
            Hosting = CreateHostingOptions(),
            TickRate = 1000
        };

        var player = new PlayerState(
            Array.Empty<IPacketHandler>(),
            communicator,
            NullLogger<PlayerState>.Instance,
            Microsoft.Extensions.Options.Options.Create(serverOptions),
            new AcornMetrics(),
            sessionId,
            _ => Task.CompletedTask)
        {
            Character = character,
            CurrentMap = map
        };

        map.Players.TryAdd(sessionId, player);
        return (player, communicator);
    }
}
