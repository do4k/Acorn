using System.Collections.Concurrent;
using Acorn.Database.Repository;
using Acorn.Game.Models;
using Acorn.Game.Services;
using Acorn.Infrastructure.Communicators;
using Acorn.Infrastructure.Gemini;
using Acorn.Infrastructure.Telemetry;
using Acorn.Net;
using Acorn.Net.PacketHandlers;
using Acorn.Net.PacketHandlers.Player.Talk;
using Acorn.Options;
using Acorn.World.Map;
using Acorn.World.Services;
using Acorn.World.Services.Map;
using Acorn.World.Services.Npc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using NSubstitute;

namespace Acorn.Tests.TestSupport;

/// <summary>
///     In-memory communicator that records every payload the server sends, so unit
///     tests can assert whether a handler delivered a packet without a live socket.
/// </summary>
internal sealed class CapturingCommunicator : ICommunicator
{
    public List<byte[]> Sent { get; } = [];

    public bool IsConnected => false;

    public Task Send(IEnumerable<byte> bytes)
    {
        Sent.Add(bytes.ToArray());
        return Task.CompletedTask;
    }

    public Stream Receive() => Stream.Null;

    public Task CloseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public string GetConnectionOrigin() => "test";
}

/// <summary>
///     Builds a <see cref="PlayerState" /> backed by a <see cref="CapturingCommunicator" />
///     for handler-level unit tests.
/// </summary>
internal static class FakePlayer
{
    public static (PlayerState Player, CapturingCommunicator Communicator) Create(string name = "Tester",
        int sessionId = 1)
    {
        var communicator = new CapturingCommunicator();
        var player = new PlayerState(
            Enumerable.Empty<IPacketHandler>(),
            communicator,
            NullLogger<PlayerState>.Instance,
            Microsoft.Extensions.Options.Options.Create(CreateOptions()),
            new AcornMetrics(),
            sessionId,
            _ => Task.CompletedTask);

        player.Character = CreateCharacter(name);
        return (player, communicator);
    }

    public static Character CreateCharacter(string name = "Tester")
    {
        return new Character
        {
            Accounts_Username = "tester",
            Name = name,
            Inventory = new Inventory(new ConcurrentBag<ItemWithAmount>()),
            Bank = new Bank(new ConcurrentBag<ItemWithAmount>()),
            Paperdoll = new Paperdoll(),
            Spells = new Spells(new ConcurrentBag<Spell>())
        };
    }

    public static WiseManTalkHandler CreateWiseManHandler()
    {
        var agent = Substitute.For<IWiseManAgent>();
        var wiseManOptions = Microsoft.Extensions.Options.Options.Create(new WiseManAgentOptions());
        var queue = new WiseManQueueService(agent, NullLogger<WiseManQueueService>.Instance, wiseManOptions);
        return new WiseManTalkHandler(queue, NullLogger<WiseManTalkHandler>.Instance, wiseManOptions);
    }

    public static ServerOptions CreateOptions(int chatLength = 128, int chatMaxWidth = 1400)
    {
        return new ServerOptions
        {
            NewCharacter = new NewCharacterOptions { X = 6, Y = 6, Map = 1 },
            Hosting = new HostingOptions
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
                Port = 1,
                WebSocketPort = 2
            },
            TickRate = 1000,
            ChatLength = chatLength,
            ChatMaxWidth = chatMaxWidth
        };
    }
}

/// <summary>
///     Builds a bare in-memory <see cref="MapState" /> (20x20, no NPCs) for testing
///     range-limited broadcasts.
/// </summary>
internal static class FakeMap
{
    public static MapState Create(int width = 20, int height = 20)
    {
        var emf = new Emf
        {
            Name = "TestMap",
            Width = width,
            Height = height,
            Npcs = new List<MapNpc>(),
            Items = new List<Moffat.EndlessOnline.SDK.Protocol.Map.MapItem>(),
            TileSpecRows = new List<MapTileSpecRow>(),
            WarpRows = new List<MapWarpRow>(),
            GraphicLayers = Enumerable.Range(0, 9).Select(_ => new MapGraphicLayer()).ToList(),
            Signs = new List<MapSign>(),
            LegacyDoorKeys = new List<MapLegacyDoorKey>(),
            Rid = new List<int> { 1, 2 }
        };

        return new MapState(
            new MapWithId(1, emf),
            Substitute.For<IDataFileRepository>(),
            Substitute.For<IMapBroadcastService>(),
            Substitute.For<IMapController>(),
            Substitute.For<INpcController>(),
            Substitute.For<IPaperdollService>(),
            90,
            false,
            30,
            NullLogger<MapState>.Instance);
    }
}
