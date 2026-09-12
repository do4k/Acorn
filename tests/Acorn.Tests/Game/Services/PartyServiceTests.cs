using Acorn.Database.Repository;
using Acorn.Game.Models;
using Acorn.Game.Services;
using Acorn.Infrastructure.Communicators;
using Acorn.Infrastructure.Telemetry;
using Acorn.Net;
using Acorn.Net.PacketHandlers;
using Acorn.Options;
using Acorn.World;
using Acorn.World.Map;
using Acorn.World.Services.Map;
using Acorn.World.Services.Npc;
using Acorn.World.Services.Party;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moffat.EndlessOnline.SDK.Data;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using NSubstitute;
using System.Threading.Tasks;

namespace Acorn.Tests.Game.Services;

public class PartyServiceTests
{
    [Test]
    public async Task RemoveFromParty_WhenLeaderLeavesTwoMemberParty_ShouldSendPartyClose()
    {
        // Arrange
        var map = CreateMap();
        var (leader, leaderComm) = CreatePlayer(100, map);
        var (member, memberComm) = CreatePlayer(200, map);
        var (service, tileService) = CreateService(map, leader, member);
        tileService.InClientRange(Arg.Any<Coords>(), Arg.Any<Coords>()).Returns(true);

        await service.RequestParty(leader, member.SessionId, PartyRequestType.Invite);
        await service.AcceptPartyRequest(member, leader.SessionId, PartyRequestType.Invite);

        leaderComm.Sent.Clear();
        memberComm.Sent.Clear();

        // Act - the leader leaves a two-member party, which disbands it.
        await service.RemoveFromParty(leader, leader.SessionId);

        // Assert - Party/Close (not Party/Remove) reaches every member.
        var leaderActions = leaderComm.Sent.Select(Decode).ToList();
        var memberActions = memberComm.Sent.Select(Decode).ToList();

        leaderActions.Should().Contain((PacketAction.Close, PacketFamily.Party));
        memberActions.Should().Contain((PacketAction.Close, PacketFamily.Party));
        leaderActions.Should().NotContain(x => x.Action == PacketAction.Remove);
        memberActions.Should().NotContain(x => x.Action == PacketAction.Remove);
    }

    [Test]
    public async Task RequestParty_WhenTargetOutOfRange_ShouldNotSendRequest()
    {
        // Arrange
        var map = CreateMap();
        var (leader, _) = CreatePlayer(100, map);
        var (member, memberComm) = CreatePlayer(200, map);
        var (service, tileService) = CreateService(map, leader, member);
        tileService.InClientRange(Arg.Any<Coords>(), Arg.Any<Coords>()).Returns(false);

        // Act
        await service.RequestParty(leader, member.SessionId, PartyRequestType.Invite);

        // Assert
        memberComm.Sent.Should().BeEmpty("out-of-range invites must be ignored");
    }

    [Test]
    public async Task AcceptPartyRequest_WhenPlayersNoLongerInRange_ShouldNotCreateParty()
    {
        // Arrange
        var map = CreateMap();
        var (leader, leaderComm) = CreatePlayer(100, map);
        var (member, memberComm) = CreatePlayer(200, map);
        var (service, tileService) = CreateService(map, leader, member);
        tileService.InClientRange(Arg.Any<Coords>(), Arg.Any<Coords>()).Returns(true);

        await service.RequestParty(leader, member.SessionId, PartyRequestType.Invite);

        // The accepter moved away after the request was made.
        tileService.InClientRange(Arg.Any<Coords>(), Arg.Any<Coords>()).Returns(false);

        // Act
        await service.AcceptPartyRequest(member, leader.SessionId, PartyRequestType.Invite);

        // Assert
        leaderComm.Sent.Should().NotContain(x => Decode(x).Action == PacketAction.Create);
        memberComm.Sent.Should().NotContain(x => Decode(x).Action == PacketAction.Create);
    }

    // --- Helpers ---

    private static (PartyService Service, IMapTileService TileService) CreateService(
        MapState map, PlayerState leader, PlayerState member)
    {
        var worldState = CreateWorldState();
        worldState.TryAddPlayer(leader.SessionId, leader).Should().BeTrue();
        worldState.TryAddPlayer(member.SessionId, member).Should().BeTrue();

        var tileService = Substitute.For<IMapTileService>();

        var service = new PartyService(
            worldState,
            Substitute.For<IFormulaService>(),
            Substitute.For<IDataFileRepository>(),
            Microsoft.Extensions.Options.Options.Create(new PartyOptions()),
            tileService,
            NullLogger<PartyService>.Instance);

        return (service, tileService);
    }

    private static WorldState CreateWorldState()
    {
        var dataFiles = Substitute.For<IDataFileRepository>();
        dataFiles.Maps.Returns(Array.Empty<MapWithId>());
        return new WorldState(dataFiles, null!, NullLogger<WorldState>.Instance);
    }

    private static MapState CreateMap()
    {
        return new MapState(
            new MapWithId(1, new Emf { Npcs = [] }),
            Substitute.For<IDataFileRepository>(),
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

    private static (PlayerState Player, CapturingCommunicator Communicator) CreatePlayer(int sessionId, MapState map)
    {
        var communicator = new CapturingCommunicator();
        var options = Microsoft.Extensions.Options.Options.Create(new ServerOptions
        {
            NewCharacter = new NewCharacterOptions { X = 6, Y = 6, Map = 1 },
            Hosting = new HostingOptions
            {
                SLN = new SLNOptions
                {
                    Enabled = false,
                    Url = "",
                    PingRate = 5,
                    UserAgent = "",
                    Zone = "",
                    ServerName = "",
                    Site = ""
                },
                HostName = "localhost",
                Port = 0,
                WebSocketPort = 0
            },
            TickRate = 1000
        });

        var player = new PlayerState(
            Enumerable.Empty<IPacketHandler>(),
            communicator,
            NullLogger<PlayerState>.Instance,
            options,
            new AcornMetrics(),
            sessionId,
            _ => Task.CompletedTask)
        {
            Character = new Character
            {
                Accounts_Username = "test",
                Name = $"player{sessionId}",
                Map = 1,
                X = 6,
                Y = 6,
                Level = 1,
                MaxHp = 100,
                Hp = 100,
                Inventory = new Inventory([]),
                Bank = new Bank([]),
                Paperdoll = new Paperdoll(),
                Spells = new Spells([])
            },
            CurrentMap = map,
            ServerEncryptionMulti = 1
        };

        map.Players.TryAdd(sessionId, player);
        return (player, communicator);
    }

    private static (PacketAction Action, PacketFamily Family) Decode(byte[] frame)
    {
        // PlayerState.Send frames packets as [2-byte length][encrypted action][family][payload].
        var encrypted = frame.Skip(2).ToArray();
        var raw = DataEncrypter.SwapMultiples(
            DataEncrypter.Deinterleave(DataEncrypter.FlipMSB(encrypted)), 1);
        return ((PacketAction)raw[0], (PacketFamily)raw[1]);
    }

    private sealed class CapturingCommunicator : ICommunicator
    {
        public List<byte[]> Sent { get; } = [];

        public bool IsConnected => false;

        public Task Send(IEnumerable<byte> bytes)
        {
            Sent.Add(bytes.ToArray());
            return Task.CompletedTask;
        }

        public Stream Receive() => new MemoryStream();

        public Task CloseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public string GetConnectionOrigin() => "test";
    }
}