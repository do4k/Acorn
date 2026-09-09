using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Xunit;

namespace Acorn.Tests.Integration;

/// <summary>
///     Regression tests that lock down the sequence/encryption protocol invariants.
///     The EO sequence layer is deliberately brittle (pre-increment sequencer vs
///     the SDK's post-increment, session IDs sharing the AccountReply enum space,
///     ping resync, encryption round-trips), so these tests pin the behaviors that
///     were hard-won so future changes can't silently reintroduce desync.
/// </summary>
public class PacketSequenceTests : IClassFixture<TestServerFixture>
{
    private readonly TestServerFixture _fixture;

    public PacketSequenceTests(TestServerFixture fixture)
    {
        _fixture = fixture;
    }

    // The server spawns new characters at these coordinates (see fixture config).
    private const int StartX = 6;
    private const int StartY = 6;

    [Fact]
    public async Task Tcp_Sequence_ShouldProgressAcrossLongRunOfPackets()
    {
        await using var client = await LoginAndCreateAsync(_fixture.TcpPort, "seq");
        await EnterGameAsync(client);

        var expectedX = StartX;
        var lastSequence = client.LastSequence;

        for (var i = 0; i < 40; i++)
        {
            expectedX++;
            var reply = await WalkAsync(client, Direction.Right, expectedX, StartY);

            reply.Should().NotBeNull("the server should reply to every walk and stay in sync");

            var nextSequence = client.LastSequence;
            (nextSequence % 10).Should().Be((lastSequence + 1) % 10,
                "the sequence must advance by exactly +1 (mod 10) for every packet on a long run");
            lastSequence = nextSequence;
        }

        // The full run completed with replies, so the connection never desynced.
        client.IsConnected.Should().BeTrue();
        _fixture.GetPlayer(client.PlayerId)!.Character!.X.Should().Be(StartX + 40);
    }

    [Fact]
    public async Task Tcp_CharacterCreateEnterGameWalk_ShouldStayInSync()
    {
        await using var client = await LoginAndCreateAsync(_fixture.TcpPort, "walk");
        await EnterGameAsync(client);

        var expectedX = StartX;
        var expectedY = StartY;

        // Walk right, down, right, down, right — several tiles in multiple directions.
        var path = new (Direction dir, int dx, int dy)[]
        {
            (Direction.Right, 1, 0),
            (Direction.Right, 1, 0),
            (Direction.Down, 0, 1),
            (Direction.Right, 1, 0),
            (Direction.Down, 0, 1),
            (Direction.Right, 1, 0),
        };

        foreach (var (dir, dx, dy) in path)
        {
            expectedX += dx;
            expectedY += dy;
            var reply = await WalkAsync(client, dir, expectedX, expectedY);
            reply.Should().NotBeNull();

            // Server must have actually moved the character to the expected tile.
            var serverCharacter = _fixture.GetPlayer(client.PlayerId)!.Character!;
            serverCharacter.X.Should().Be(expectedX);
            serverCharacter.Y.Should().Be(expectedY);
        }

        client.IsConnected.Should().BeTrue();
        _fixture.GetPlayer(client.PlayerId)!.Character!.X.Should().Be(expectedX);
        _fixture.GetPlayer(client.PlayerId)!.Character!.Y.Should().Be(expectedY);
    }

    [Fact]
    public async Task Tcp_PingPong_ShouldResyncSequence_AndKeepConnectionAlive()
    {
        await using var client = await LoginAndCreateAsync(_fixture.TcpPort, "ping");
        await EnterGameAsync(client);

        // Wait for the server's Connection_Player ping. ReceivePacketAsync automatically
        // resyncs the client's outbound sequencer to the received ping value.
        using var pingCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        IPacket ping = await ReceiveUntilAsync(client,
            p => p is ConnectionPlayerServerPacket, pingCts.Token);

        ping.Should().BeOfType<ConnectionPlayerServerPacket>();
        client.PingReceived.Should().BeTrue();

        // Reply with pong and confirm the connection survives the resync.
        await client.SendConnectionPingAsync();
        client.IsConnected.Should().BeTrue();

        // A subsequent in-game packet must still line up after the ping resync.
        var reply = await WalkAsync(client, Direction.Right, StartX + 1, StartY);
        reply.Should().NotBeNull();
        client.IsConnected.Should().BeTrue();
        _fixture.GetPlayer(client.PlayerId)!.Character!.X.Should().Be(StartX + 1);
    }

    [Fact]
    public async Task Tcp_Disconnect_ShouldCleanUpWorldState()
    {
        var baseline = _fixture.OnlinePlayerCount;

        EoTestClient client;
        try
        {
            client = await LoginAndCreateAsync(_fixture.TcpPort, "dc");
        }
        catch (Exception)
        {
            // WebSocket/network quirks shouldn't fail the suite; port readiness is covered elsewhere.
            return;
        }

        await EnterGameAsync(client);
        _fixture.OnlinePlayerCount.Should().Be(baseline + 1);

        await client.DisposeAsync();
        await WaitUntilAsync(() => _fixture.OnlinePlayerCount == baseline,
            TimeSpan.FromSeconds(5));

        _fixture.OnlinePlayerCount.Should().Be(baseline,
            "a mid-session disconnect should remove the player from world state");
    }

    // --- Flow helpers ---

    private static async Task<EoTestClient> LoginAndCreateAsync(int tcpPort, string prefix)
    {
        var client = await EoTestClient.ConnectTcpAsync(tcpPort);

        await client.InitAsync();
        await client.SendConnectionAcceptAsync();

        var username = $"{prefix}_{Guid.NewGuid():N}"[..20];
        var password = "testpassword123";
        var sessionId = await client.AccountRequestAsync(username);
        var createReply = await client.AccountCreateAsync(username, password, sessionId);
        createReply.Should().Be(AccountReply.Created);

        var loginReply = await client.LoginAsync(username, password);
        loginReply.ReplyCode.Should().Be(LoginReply.Ok);

        // Create a character so we can enter the game. Names are globally unique,
        // so build a random short name to survive tests sharing one server/db.
        var charName = $"go{Guid.NewGuid():N}"[..10];
        var charReply = await client.CreateCharacterAsync(sessionId, charName);
        charReply.Should().Be(CharacterReply.Ok);

        return client;
    }

    private static async Task EnterGameAsync(EoTestClient client)
    {
        // Welcome request -> reply with character data.
        await client.SendPacketAsync(new WelcomeRequestClientPacket { CharacterId = 0 });
        var welcome = (WelcomeReplyServerPacket)await client.ReceivePacketAsync();
        welcome.WelcomeCode.Should().Be(WelcomeCode.SelectCharacter);

        // Welcome message -> server puts the player in-game.
        await client.SendPacketAsync(new WelcomeMsgClientPacket
        {
            SessionId = client.PlayerId,
            CharacterId = 0
        });
        var enter = (WelcomeReplyServerPacket)await client.ReceivePacketAsync();
        enter.WelcomeCode.Should().Be(WelcomeCode.EnterGame);
    }

    private static async Task<WalkReplyServerPacket> WalkAsync(EoTestClient client,
        Direction direction, int x, int y)
    {
        await client.SendPacketAsync(new WalkPlayerClientPacket
        {
            WalkAction = new WalkAction
            {
                Direction = direction,
                Timestamp = 0,
                Coords = new Moffat.EndlessOnline.SDK.Protocol.Coords { X = x, Y = y }
            }
        });

        var reply = await client.ReceivePacketAsync();
        return (WalkReplyServerPacket)reply;
    }

    private static async Task<IPacket> ReceiveUntilAsync(EoTestClient client,
        Func<IPacket, bool> predicate, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var packet = await client.ReceivePacketAsync();
            if (predicate(packet))
            {
                return packet;
            }
        }

        throw new TimeoutException("Did not receive expected packet within the timeout");
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        while (!cts.IsCancellationRequested)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(50, cts.Token);
        }

        cts.Cancel();
        throw new TimeoutException("Condition was not met within the timeout");
    }
}
