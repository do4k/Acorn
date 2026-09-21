using Acorn.Tests.Support;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using System.Threading.Tasks;

namespace Acorn.Tests.Integration;

/// <summary>
///     End-to-end coverage for map interactions (doors and chests). These verify the
///     handlers use the coordinates from the packet, validate adjacency/range, and emit
///     the protocol-correct packets.
/// </summary>
[ClassDataSource<TestServerFixture>(Shared = SharedType.PerClass)]
[NotInParallel("MapInteractionTests")]
public class MapInteractionTests
{
    private const int StartX = 6;
    private const int StartY = 6;
    private const int ChestX = 5;
    private const int ChestY = 6;
    private const int FarChestX = 15;
    private const int FarChestY = 15;
    private const int UnlockedDoorX = 6;
    private const int UnlockedDoorY = 5;
    private const int LockedDoorX = 5;
    private const int LockedDoorY = 5;

    // A dedicated unlocked door that actually warps to map 2, used to verify the
    // full open-then-walk-through flow.
    private const int WalkDoorX = 9;
    private const int WalkDoorY = 5;
    private const int WalkDoorTargetMap = 2;
    private const int WalkDoorTargetX = 5;
    private const int WalkDoorTargetY = 5;

    private readonly TestServerFixture _fixture;

    public MapInteractionTests(TestServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Test]
    public async Task ChestOpen_WhenAdjacent_ShouldReturnChestOpen()
    {
        await using var client = await LoginAndEnterAsync("chestopen");

        await client.SendPacketAsync(new ChestOpenClientPacket
        {
            Coords = new Coords { X = ChestX, Y = ChestY }
        });

        var reply = await ReceiveUntilAsync(client, p => p is ChestOpenServerPacket) as ChestOpenServerPacket;

        reply.Should().NotBeNull();
        reply!.Coords.X.Should().Be(ChestX);
        reply.Coords.Y.Should().Be(ChestY);

        _fixture.GetMap(1)!.Chests.Keys.Should().Contain(c => c.X == ChestX && c.Y == ChestY);
    }

    [Test]
    public async Task ChestOpen_WhenNotAdjacent_ShouldBeIgnored()
    {
        await using var client = await LoginAndEnterAsync("chestfar");

        await client.SendPacketAsync(new ChestOpenClientPacket
        {
            Coords = new Coords { X = FarChestX, Y = FarChestY }
        });

        // Give the server a moment to (incorrectly) create/open the chest.
        await Task.Delay(300);

        _fixture.GetMap(1)!.Chests.Keys.Should().NotContain(c => c.X == FarChestX && c.Y == FarChestY,
            "the player is not adjacent to the far chest");
    }

    [Test]
    public async Task DoorOpen_WhenAdjacentUnlocked_ShouldBroadcastDoorOpen()
    {
        await using var client = await LoginAndEnterAsync("dooropen");

        await client.SendPacketAsync(new DoorOpenClientPacket
        {
            Coords = new Coords { X = UnlockedDoorX, Y = UnlockedDoorY }
        });

        var reply = await ReceiveUntilAsync(client, p => p is DoorOpenServerPacket) as DoorOpenServerPacket;

        reply.Should().NotBeNull();
        reply!.Coords.X.Should().Be(UnlockedDoorX);
        reply.Coords.Y.Should().Be(UnlockedDoorY);

        _fixture.GetMap(1)!.OpenedDoors.Keys.Should().Contain(c => c.X == UnlockedDoorX && c.Y == UnlockedDoorY);
    }

    [Test]
    public async Task DoorOpen_WhenLockedWithoutKey_ShouldReturnDoorClose()
    {
        await using var client = await LoginAndEnterAsync("doorlock");

        await client.SendPacketAsync(new DoorOpenClientPacket
        {
            Coords = new Coords { X = LockedDoorX, Y = LockedDoorY }
        });

        var reply = await ReceiveUntilAsync(client, p => p is DoorCloseServerPacket) as DoorCloseServerPacket;

        reply.Should().NotBeNull();
        reply!.Key.Should().Be(2, "LockedSilver is door spec 2");

        _fixture.GetMap(1)!.OpenedDoors.Keys.Should().NotContain(c => c.X == LockedDoorX && c.Y == LockedDoorY);
    }

    [Test]
    public async Task DoorOpen_WhenWalkedThrough_ShouldWarpToDestination()
    {
        await using var client = await LoginAndEnterAsync("doorwalk");

        // Walk along the spawn row until standing directly below the door.
        for (var x = StartX + 1; x <= WalkDoorX; x++)
        {
            await SendWalkAsync(client, Direction.Right, x, StartY);
            await ReceiveUntilAsync(client, p => p is WalkReplyServerPacket);
        }

        await client.SendPacketAsync(new DoorOpenClientPacket
        {
            Coords = new Coords { X = WalkDoorX, Y = WalkDoorY }
        });
        await ReceiveUntilAsync(client, p => p is DoorOpenServerPacket);

        // Stepping onto the now-open door must warp to its destination map.
        await SendWalkAsync(client, Direction.Up, WalkDoorX, WalkDoorY);

        var request = await ReceiveUntilAsync(client, p => p is WarpRequestServerPacket) as WarpRequestServerPacket;
        request.Should().NotBeNull("walking through an open door must trigger its warp");
        request!.WarpType.Should().Be(WarpType.MapSwitch);
        request.MapId.Should().Be(WalkDoorTargetMap);

        // The map change is applied server-side before the client accepts it.
        var player = _fixture.GetPlayer(client.PlayerId)!;
        player.Character!.Map.Should().Be(WalkDoorTargetMap);
        player.Character.X.Should().Be(WalkDoorTargetX);
        player.Character.Y.Should().Be(WalkDoorTargetY);

        await client.SendPacketAsync(new WarpAcceptClientPacket
        {
            MapId = WalkDoorTargetMap,
            SessionId = client.PlayerId
        });
        await ReceiveUntilAsync(client, p => p is WarpAgreeServerPacket);
    }

    // --- Flow helpers ---

    private static Task SendWalkAsync(EoTestClient client, Direction direction, int x, int y)
    {
        return client.SendPacketAsync(new WalkPlayerClientPacket
        {
            WalkAction = new WalkAction
            {
                Direction = direction,
                Timestamp = 0,
                Coords = new Coords { X = x, Y = y }
            }
        });
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

    private static async Task<IPacket> ReceiveUntilAsync(EoTestClient client, Func<IPacket, bool> predicate)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!cts.IsCancellationRequested)
        {
            var packet = await client.ReceivePacketAsync();
            if (packet is ConnectionPlayerServerPacket)
            {
                await client.SendConnectionPingAsync();
                continue;
            }

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