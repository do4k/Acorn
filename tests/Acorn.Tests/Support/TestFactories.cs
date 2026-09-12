using System.Collections.Concurrent;
using Acorn.Database.Repository;
using Acorn.Game.Models;
using Acorn.Game.Services;
using Acorn.Infrastructure.Communicators;
using Acorn.Infrastructure.Telemetry;
using Acorn.Net;
using Acorn.Net.PacketHandlers;
using Acorn.Options;
using Acorn.World.Map;
using Acorn.World.Npc;
using Acorn.World.Services.Map;
using Acorn.World.Services.Npc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using NSubstitute;

namespace Acorn.Tests.Support;

/// <summary>
///     Shared helpers for constructing the world/player objects used by unit tests.
/// </summary>
internal static class TestFactories
{
    public static ServerOptions CreateServerOptions(int maxDrop = 10000)
    {
        return new ServerOptions
        {
            TickRate = 1000,
            MaxDrop = maxDrop,
            NewCharacter = new NewCharacterOptions { Map = 1, X = 1, Y = 1 },
            Hosting = new HostingOptions
            {
                HostName = "localhost",
                Port = 8078,
                WebSocketPort = 8079,
                SLN = new SLNOptions
                {
                    Enabled = false,
                    Url = "http://localhost",
                    PingRate = 5,
                    UserAgent = "Test",
                    Zone = "Test",
                    ServerName = "Test",
                    Site = "http://localhost"
                }
            }
        };
    }

    public static MapState CreateMap(int id = 1, int width = 20, int height = 20,
        IMapBroadcastService? broadcastService = null)
    {
        var emf = new Emf
        {
            Width = width,
            Height = height,
            Npcs = new List<MapNpc>(),
            TileSpecRows = new List<MapTileSpecRow>()
        };

        var dataRepository = Substitute.For<IDataFileRepository>();
        dataRepository.Enf.Returns(new Enf
        {
            Rid = new List<int> { 1 },
            Version = 1,
            TotalNpcsCount = 0,
            Npcs = new List<EnfRecord>()
        });

        var npcController = Substitute.For<INpcController>();
        npcController.ShouldUseSpawnVariance(Arg.Any<NpcState>()).Returns(false);

        return new MapState(
            new MapWithId(id, emf),
            dataRepository,
            broadcastService ?? Substitute.For<IMapBroadcastService>(),
            Substitute.For<IMapController>(),
            npcController,
            Substitute.For<IMapTileService>(),
            Substitute.For<IPaperdollService>(),
            playerRecoverRate: 90,
            isArenaEnabled: false,
            arenaSpawnInterval: 30,
            NullLogger<MapState>.Instance);
    }

    public static PlayerState CreatePlayer(int sessionId, int x, int y, ServerOptions? options = null)
    {
        var communicator = Substitute.For<ICommunicator>();
        communicator.IsConnected.Returns(false);

        return new PlayerState(
            Enumerable.Empty<IPacketHandler>(),
            communicator,
            NullLogger<PlayerState>.Instance,
            Microsoft.Extensions.Options.Options.Create(options ?? CreateServerOptions()),
            new AcornMetrics(),
            sessionId,
            _ => Task.CompletedTask)
        {
            Character = new Character
            {
                Accounts_Username = "test",
                Name = $"Player{sessionId}",
                Map = 1,
                X = x,
                Y = y,
                Direction = Direction.Down,
                Inventory = new Inventory(new ConcurrentBag<ItemWithAmount>()),
                Bank = new Bank(new ConcurrentBag<ItemWithAmount>()),
                Paperdoll = new Paperdoll(),
                Spells = new Spells(new ConcurrentBag<Spell>())
            }
        };
    }
}
