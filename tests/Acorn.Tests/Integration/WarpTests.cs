using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Xunit;

namespace Acorn.Tests.Integration;

/// <summary>
///     Covers map warps. A map-switch warp must apply the map change exactly once
///     (a single leave on the old map, a single enter on the new map) and must always
///     clear the warp session, including for local warps.
/// </summary>
public class WarpTests : IClassFixture<TestServerFixture>
{
    // The server spawns new characters at these coordinates (see fixture config).
    private const int StartX = 6;
    private const int StartY = 6;

    // The fixture places a map-switch warp to map 2 at (7,8).
    private const int WarpTileX = 7;
    private const int WarpTileY = 8;
    private const int TargetMap = 2;
    private const int TargetX = 5;
    private const int TargetY = 5;

    private readonly TestServerFixture _fixture;

    public WarpTests(TestServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MapSwitchWarp_ShouldMovePlayerToTargetMapExactlyOnce()
    {
        await WaitForNoPlayersAsync();
        await using var client = await LoginAndEnterAsync("warp");

        // Walk around to the tile just before the warp, then step onto it.
        await WalkAsync(client, Direction.Right, WarpTileX, StartY);
        await WalkAsync(client, Direction.Down, WarpTileX, StartY + 1);

        var request = await WalkAndReceiveWarpRequestAsync(client, Direction.Down, WarpTileX, WarpTileY);
        request.WarpType.Should().Be(WarpType.MapSwitch);
        request.MapId.Should().Be(TargetMap);

        // The map change is applied server-side before the client accepts the warp
        // (matches eoserv Character::Warp), so the world state is already updated.
        var player = _fixture.GetPlayer(client.PlayerId)!;
        player.Character!.Map.Should().Be(TargetMap);
        player.Character.X.Should().Be(TargetX);
        player.Character.Y.Should().Be(TargetY);

        await AcceptWarpAsync(client, TargetMap);

        // The warp session is cleared and the player is on the target map only.
        player.WarpSession.Should().BeNull("the warp session must be cleared after a map switch");
        _fixture.GetMap(TargetMap)!.Players.Should().ContainKey(client.PlayerId);
        _fixture.GetMap(1)!.Players.Should().NotContainKey(client.PlayerId);
    }

    [Fact]
    public async Task MapSwitchWarp_ShouldSendSingleEnterToOtherPlayers()
    {
        await WaitForNoPlayersAsync();
        await using var observer = await LoginAndEnterAsync("obs");
        await using var warper = await LoginAndEnterAsync("wrp");

        // Put the observer on the target map first so they can watch the arrival.
        await WarpToTargetMapAsync(observer);

        var received = new List<IPacket>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var observerTask = Task.Run(async () =>
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    var packet = await observer.ReceivePacketAsync();
                    if (packet is ConnectionPlayerServerPacket)
                    {
                        await observer.SendConnectionPingAsync();
                        continue;
                    }

                    lock (received)
                    {
                        received.Add(packet);
                    }
                }
            }
            catch
            {
                // Cancellation / socket teardown ends the observation loop.
            }
        });

        await WarpToTargetMapAsync(warper);

        await WaitUntilAsync(() =>
        {
            lock (received)
            {
                return received.Any(p => p is PlayersAgreeServerPacket);
            }
        }, TimeSpan.FromSeconds(5));

        // Give any (incorrect) duplicate enter/leave packets a moment to arrive.
        await Task.Delay(300);
        cts.Cancel();
        await Task.WhenAny(observerTask, Task.Delay(500));

        lock (received)
        {
            received.OfType<PlayersAgreeServerPacket>().Should().HaveCount(1,
                "other players must see exactly one enter for a map-switch warp");
            received.OfType<PlayersRemoveServerPacket>().Should().BeEmpty(
                "other players must not see a leave during a map-switch warp");
        }
    }

    // --- Flow helpers ---

    private async Task<EoTestClient> LoginAndEnterAsync(string prefix)
    {
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

    private static async Task WarpToTargetMapAsync(EoTestClient client)
    {
        await WalkAsync(client, Direction.Right, WarpTileX, StartY);
        await WalkAsync(client, Direction.Down, WarpTileX, StartY + 1);

        var request = await WalkAndReceiveWarpRequestAsync(client, Direction.Down, WarpTileX, WarpTileY);
        request.MapId.Should().Be(TargetMap);

        await AcceptWarpAsync(client, TargetMap);
    }

    private static async Task<WarpRequestServerPacket> WalkAndReceiveWarpRequestAsync(
        EoTestClient client, Direction direction, int x, int y)
    {
        await SendWalkAsync(client, direction, x, y);
        return (WarpRequestServerPacket)await ReceiveUntilAsync(client, p => p is WarpRequestServerPacket);
    }

    private static async Task AcceptWarpAsync(EoTestClient client, int mapId)
    {
        await client.SendPacketAsync(new WarpAcceptClientPacket
        {
            MapId = mapId,
            SessionId = client.PlayerId
        });

        await ReceiveUntilAsync(client, p => p is WarpAgreeServerPacket);
    }

    private static async Task WalkAsync(EoTestClient client, Direction direction, int x, int y)
    {
        await SendWalkAsync(client, direction, x, y);
        await ReceiveUntilAsync(client, p => p is WalkReplyServerPacket);
    }

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

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        while (!cts.IsCancellationRequested)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25, cts.Token);
        }

        cts.Cancel();
        throw new TimeoutException("Condition was not met within the timeout");
    }

    private async Task WaitForNoPlayersAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!cts.IsCancellationRequested && _fixture.OnlinePlayerCount > 0)
        {
            await Task.Delay(25, cts.Token);
        }
    }
}
