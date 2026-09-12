using Acorn.World.Map;
using Acorn.World.Npc;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Moffat.EndlessOnline.SDK.Protocol.Pub;
using Xunit;
using PubNpcType = Moffat.EndlessOnline.SDK.Protocol.Pub.NpcType;

namespace Acorn.Tests.Integration;

/// <summary>
///     Covers server-authoritative walk validation: out-of-bounds and unwalkable
///     destinations are refused, the server position is left untouched, and the
///     client is refreshed. A normal walk must still work.
/// </summary>
public class WalkValidationTests : IClassFixture<TestServerFixture>
{
    private const int StartX = 6;
    private const int StartY = 6;

    // The shared test map has a non-walkable chair at (6, 11).
    private const int ChairX = 6;
    private const int ChairY = 11;

    private readonly TestServerFixture _fixture;

    public WalkValidationTests(TestServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task NormalWalk_ShouldMoveThePlayer()
    {
        await using var client = await LoginAndEnterAsync("walkok");

        var reply = await SendWalkAsync(client, Direction.Right, StartX + 1, StartY);

        reply.Should().BeOfType<WalkReplyServerPacket>();

        var character = _fixture.GetPlayer(client.PlayerId)!.Character!;
        character.X.Should().Be(StartX + 1);
        character.Y.Should().Be(StartY);
    }

    [Fact]
    public async Task WalkIntoUnwalkableTile_ShouldBeRejected_AndKeepServerPosition()
    {
        await using var client = await LoginAndEnterAsync("walkwall");

        // Walk down to (6, 10), immediately above the chair at (6, 11).
        for (var y = StartY + 1; y <= ChairY - 1; y++)
        {
            var step = await SendWalkAsync(client, Direction.Down, ChairX, y);
            step.Should().BeOfType<WalkReplyServerPacket>();
        }

        _fixture.GetPlayer(client.PlayerId)!.Character!.Y.Should().Be(ChairY - 1);

        // Stepping onto the chair must be refused.
        var rejected = await SendWalkAsync(client, Direction.Down, ChairX, ChairY);

        rejected.Should().BeOfType<WarpRequestServerPacket>("the server refuses the move and refreshes the client");
        ((WarpRequestServerPacket)rejected).WarpType.Should().Be(WarpType.Local);

        var character = _fixture.GetPlayer(client.PlayerId)!.Character!;
        character.X.Should().Be(ChairX, "the blocked walk must not change the server position");
        character.Y.Should().Be(ChairY - 1);

        // Complete the refresh; the client must be told where it actually is.
        await client.SendPacketAsync(new WarpAcceptClientPacket
        {
            MapId = character.Map,
            SessionId = client.PlayerId
        });

        var agree = await ReceiveUntilAsync(client, p => p is WarpAgreeServerPacket);
        ((WarpAgreeServerPacket)agree).WarpType.Should().Be(WarpType.Local);

        _fixture.GetPlayer(client.PlayerId)!.Character!.Y.Should().Be(ChairY - 1);
    }

    [Fact]
    public async Task WalkOutOfBounds_ShouldBeRejected_AndKeepServerPosition()
    {
        await using var client = await LoginAndEnterAsync("walkbounds");

        // Step off the spawn row first: the map interaction tests place a chest at
        // (5, 6), directly left of spawn, so walk left along y = StartY + 1 instead.
        var down = await SendWalkAsync(client, Direction.Down, StartX, StartY + 1);
        down.Should().BeOfType<WalkReplyServerPacket>();

        // Walk left to the map edge at x = 0.
        for (var x = StartX - 1; x >= 0; x--)
        {
            var step = await SendWalkAsync(client, Direction.Left, x, StartY + 1);
            step.Should().BeOfType<WalkReplyServerPacket>();
        }

        _fixture.GetPlayer(client.PlayerId)!.Character!.X.Should().Be(0);

        // One more step left is off the map and must be refused. The destination
        // cannot be encoded as a negative coordinate, but the direction alone is
        // enough for the server to compute the out-of-bounds target.
        var rejected = await SendWalkAsync(client, Direction.Left, 0, StartY + 1);

        rejected.Should().BeOfType<WarpRequestServerPacket>();
        _fixture.GetPlayer(client.PlayerId)!.Character!.X.Should().Be(0,
            "walking off the map must not change the server position");
    }

    [Fact]
    public async Task WalkWithDesyncedCoords_ShouldApplyServerMove_AndRefresh()
    {
        await using var client = await LoginAndEnterAsync("walkdesync");

        // The client claims it ended up at (9, 9) after stepping right. The server
        // sends the WalkReply first and then a refresh because of the mismatch.
        var response = await SendWalkUntilAsync(client, Direction.Right, StartX + 3, StartY + 3,
            p => p is WarpRequestServerPacket);

        response.Should().BeOfType<WarpRequestServerPacket>("the mismatch forces a refresh");

        var character = _fixture.GetPlayer(client.PlayerId)!.Character!;
        character.X.Should().Be(StartX + 1, "the server applies its own authoritative step");
        character.Y.Should().Be(StartY);
    }

    [Fact]
    public async Task WalkOntoDeadNpcTile_ShouldBeAllowed()
    {
        await using var client = await LoginAndEnterAsync("walkdead");

        var map = _fixture.GetMap(1)!;
        var npc = AddStationaryNpc(map, StartX + 1, StartY, isDead: true);

        try
        {
            var reply = await SendWalkAsync(client, Direction.Right, StartX + 1, StartY);

            reply.Should().BeOfType<WalkReplyServerPacket>("a dead NPC no longer blocks its tile");

            var character = _fixture.GetPlayer(client.PlayerId)!.Character!;
            character.X.Should().Be(StartX + 1);
            character.Y.Should().Be(StartY);
        }
        finally
        {
            map.RemoveNpc(npc);
        }
    }

    [Fact]
    public async Task WalkOntoLivingNpcTile_ShouldBeRejected()
    {
        await using var client = await LoginAndEnterAsync("walknpc");

        var map = _fixture.GetMap(1)!;
        var npc = AddStationaryNpc(map, StartX + 1, StartY, isDead: false);

        try
        {
            var reply = await SendWalkAsync(client, Direction.Right, StartX + 1, StartY);

            reply.Should().BeOfType<WarpRequestServerPacket>("a living NPC still blocks its tile");

            var character = _fixture.GetPlayer(client.PlayerId)!.Character!;
            character.X.Should().Be(StartX, "the blocked walk must not change the server position");
            character.Y.Should().Be(StartY);
        }
        finally
        {
            map.RemoveNpc(npc);
        }
    }

    // --- Flow helpers ---

    private static NpcState AddStationaryNpc(MapState map, int x, int y, bool isDead)
    {
        var index = map.GetNextNpcIndex();
        var npc = new NpcState(new EnfRecord
        {
            Name = "TestNpc",
            Hp = 100,
            Level = 1,
            Type = PubNpcType.Passive
        })
        {
            Index = index,
            Id = 1,
            X = x,
            Y = y,
            Hp = 100,
            BehaviorType = NpcBehaviorType.Stationary,
            IsDead = isDead
        };

        map.Npcs[index] = npc;
        return npc;
    }

    private async Task<EoTestClient> LoginAndEnterAsync(string prefix)
    {
        await WaitForNoPlayersAsync();

        var client = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);

        await client.InitAsync();
        await client.SendConnectionAcceptAsync();

        var username = $"{prefix}_{Guid.NewGuid():N}"[..20];
        var password = "testpassword123";
        var sessionId = await client.AccountRequestAsync(username);
        (await client.AccountCreateAsync(username, password, sessionId)).Should().Be(AccountReply.Created);
        (await client.LoginAsync(username, password)).ReplyCode.Should().Be(LoginReply.Ok);

        var charName = $"go{Guid.NewGuid():N}"[..10];
        (await client.CreateCharacterAsync(sessionId, charName)).Should().Be(CharacterReply.Ok);

        await client.SendPacketAsync(new WelcomeRequestClientPacket { CharacterId = 0 });
        var welcome = (WelcomeReplyServerPacket)await client.ReceivePacketAsync();
        welcome.WelcomeCode.Should().Be(WelcomeCode.SelectCharacter);

        await client.SendPacketAsync(new WelcomeMsgClientPacket { SessionId = client.PlayerId, CharacterId = 0 });
        var enter = (WelcomeReplyServerPacket)await client.ReceivePacketAsync();
        enter.WelcomeCode.Should().Be(WelcomeCode.EnterGame);

        return client;
    }

    private static Task<IPacket> SendWalkAsync(EoTestClient client, Direction direction, int x, int y)
    {
        return SendWalkUntilAsync(client, direction, x, y,
            p => p is WalkReplyServerPacket or WarpRequestServerPacket);
    }

    private static async Task<IPacket> SendWalkUntilAsync(EoTestClient client, Direction direction, int x, int y,
        Func<IPacket, bool> predicate)
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

        return await ReceiveUntilAsync(client, predicate);
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
