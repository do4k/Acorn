using Acorn.Tests.Support;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using System.Threading.Tasks;

namespace Acorn.Tests.Integration;

/// <summary>
///     Covers sitting/standing. The server must use the packet family and action the
///     official client expects: Sit/Reply + Sit/Player for the floor, Chair/Reply +
///     Chair/Player for chairs, and the Close/Remove packets when standing.
/// </summary>
[ClassDataSource<TestServerFixture>(Shared = SharedType.PerClass)]
[NotInParallel("SitStandTests")]
public class SitStandTests
{
    private const int StartX = 6;
    private const int StartY = 6;

    private readonly TestServerFixture _fixture;

    public SitStandTests(TestServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Test]
    public async Task FloorSit_ShouldSendSitReply_AndSetFloorState()
    {
        await using var client = await LoginAndEnterAsync("sitfloor");

        await client.SendPacketAsync(new SitRequestClientPacket
        {
            SitAction = SitAction.Sit,
            SitActionData = new SitRequestClientPacket.SitActionDataSit
            {
                CursorCoords = new Coords { X = StartX, Y = StartY }
            }
        });

        var reply = await ReceiveUntilAsync(client, p => p is SitReplyServerPacket) as SitReplyServerPacket;

        reply.Should().NotBeNull();
        reply!.PlayerId.Should().Be(client.PlayerId);
        reply.Coords.X.Should().Be(StartX);
        reply.Coords.Y.Should().Be(StartY);

        _fixture.GetPlayer(client.PlayerId)!.Character!.SitState.Should().Be(SitState.Floor);
    }

    [Test]
    public async Task FloorStand_ShouldSendSitClose_AndSetStandState()
    {
        await using var client = await LoginAndEnterAsync("sitstand");
        await SitFloorAsync(client);

        // Sit/Request is rate limited (500ms) and sit/stand share the same action.
        await Task.Delay(600);
        await client.SendPacketAsync(new SitRequestClientPacket { SitAction = SitAction.Stand });

        var reply = await ReceiveUntilAsync(client, p => p is SitCloseServerPacket) as SitCloseServerPacket;

        reply.Should().NotBeNull();
        reply!.PlayerId.Should().Be(client.PlayerId);

        var character = _fixture.GetPlayer(client.PlayerId)!.Character!;
        character.SitState.Should().Be(SitState.Stand);
        character.X.Should().Be(StartX, "standing up from the floor does not move the player");
        character.Y.Should().Be(StartY);
        reply.Coords.X.Should().Be(StartX);
        reply.Coords.Y.Should().Be(StartY);
    }

    [Test]
    public async Task SittingPlayer_ShouldNotBeAbleToWalk()
    {
        await using var client = await LoginAndEnterAsync("sitwalk");
        await SitFloorAsync(client);

        // Sit/Request is rate limited (500ms) and sit/stand share the same action.
        await Task.Delay(600);

        // A sitting player's walk request must be ignored.
        await client.SendPacketAsync(new WalkPlayerClientPacket
        {
            WalkAction = new WalkAction
            {
                Direction = Direction.Right,
                Timestamp = 0,
                Coords = new Coords { X = StartX + 1, Y = StartY }
            }
        });

        await client.SendPacketAsync(new SitRequestClientPacket { SitAction = SitAction.Stand });
        var close = await ReceiveUntilAsync(client, p => p is SitCloseServerPacket) as SitCloseServerPacket;

        close.Should().NotBeNull();
        close!.Coords.X.Should().Be(StartX, "a sitting player must not move");
        close.Coords.Y.Should().Be(StartY);
    }

    [Test]
    public async Task ChairSitAndStand_ShouldSendChairPackets_AndMoveOffChair()
    {
        await using var client = await LoginAndEnterAsync("chair");

        // Walk down to (6,10); the test map has a chair at (6,11).
        for (var y = StartY + 1; y <= 10; y++)
        {
            await WalkAsync(client, Direction.Down, StartX, y);
        }

        await client.SendPacketAsync(new ChairRequestClientPacket
        {
            SitAction = SitAction.Sit,
            SitActionData = new ChairRequestClientPacket.SitActionDataSit
            {
                Coords = new Coords { X = StartX, Y = 11 }
            }
        });

        var sitReply = await ReceiveUntilAsync(client, p => p is ChairReplyServerPacket) as ChairReplyServerPacket;

        sitReply.Should().NotBeNull();
        sitReply!.Coords.X.Should().Be(StartX);
        sitReply.Coords.Y.Should().Be(11);

        var seated = _fixture.GetPlayer(client.PlayerId)!.Character!;
        seated.SitState.Should().Be(SitState.Chair);
        seated.X.Should().Be(StartX);
        seated.Y.Should().Be(11);

        await client.SendPacketAsync(new ChairRequestClientPacket { SitAction = SitAction.Stand });

        var close = await ReceiveUntilAsync(client, p => p is ChairCloseServerPacket) as ChairCloseServerPacket;

        close.Should().NotBeNull();
        close!.Coords.X.Should().Be(StartX);
        close.Coords.Y.Should().Be(10, "standing from a chair moves one tile in the facing direction");

        var stood = _fixture.GetPlayer(client.PlayerId)!.Character!;
        stood.SitState.Should().Be(SitState.Stand);
        stood.X.Should().Be(StartX);
        stood.Y.Should().Be(10);
    }

    // --- Flow helpers ---

    private static async Task SitFloorAsync(EoTestClient client)
    {
        await client.SendPacketAsync(new SitRequestClientPacket
        {
            SitAction = SitAction.Sit,
            SitActionData = new SitRequestClientPacket.SitActionDataSit
            {
                CursorCoords = new Coords { X = StartX, Y = StartY }
            }
        });

        await ReceiveUntilAsync(client, p => p is SitReplyServerPacket);
    }

    private async Task<EoTestClient> LoginAndEnterAsync(string prefix)
    {
        // Let the previous test's disconnect cleanup (which writes to the DB) finish before
        // connecting again - the shared DbContext is not safe for concurrent operations.
        await WaitForNoPlayersAsync();

        var client = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);

        await client.InitAsync();
        await client.SendConnectionAcceptAsync();

        var username = $"{prefix}{Guid.NewGuid():N}"[..16];
        var password = TestPasswords.Valid;
        var sessionId = await client.AccountRequestAsync(username);
        (await client.AccountCreateAsync(username, password, sessionId)).Should().Be(AccountReply.Created);
        (await client.LoginAsync(username, password)).ReplyCode.Should().Be(LoginReply.Ok);

        var charName = $"go{Guid.NewGuid():N}"[..10];
        (await client.CreateCharacterAsync(sessionId, charName)).ReplyCode.Should().Be(CharacterReply.Ok);

        await client.SendPacketAsync(new WelcomeRequestClientPacket { CharacterId = client.CharacterId });
        var welcome = (WelcomeReplyServerPacket)await client.ReceivePacketAsync();
        welcome.WelcomeCode.Should().Be(WelcomeCode.SelectCharacter);

        await client.SendPacketAsync(new WelcomeMsgClientPacket { SessionId = client.PlayerId, CharacterId = client.CharacterId });
        var enter = (WelcomeReplyServerPacket)await client.ReceivePacketAsync();
        enter.WelcomeCode.Should().Be(WelcomeCode.EnterGame);

        return client;
    }

    private static async Task WalkAsync(EoTestClient client, Direction direction, int x, int y)
    {
        await client.SendPacketAsync(new WalkPlayerClientPacket
        {
            WalkAction = new WalkAction
            {
                Direction = direction,
                Timestamp = 0,
                Coords = new Coords { X = x, Y = y }
            }
        });

        await ReceiveUntilAsync(client, p => p is WalkReplyServerPacket);
    }

    private static async Task<IPacket> ReceiveUntilAsync(EoTestClient client, Func<IPacket, bool> predicate)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!cts.IsCancellationRequested)
        {
            var packet = await client.ReceivePacketAsync();
            if (predicate(packet))
            {
                return packet;
            }
        }

        throw new TimeoutException("Did not receive expected packet within the timeout");
    }

    private async Task WaitForNoPlayersAsync()
    {
        // Best-effort: disconnect cleanup is asynchronous, so give the world a moment
        // to drain before connecting. Never fail the test if a prior session is still
        // being torn down - each connection now has its own DI scope/DbContext (#76).
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (_fixture.OnlinePlayerCount > 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25);
        }
    }
}