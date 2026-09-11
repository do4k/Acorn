using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Xunit;

namespace Acorn.Tests.Integration;

/// <summary>
///     Server fixture with walk timestamp enforcement enabled (eoserv's
///     EnforceTimestamps). The default test fixture disables it so the shared
///     client helper can send a fixed timestamp of 0.
/// </summary>
public class TimestampEnforcingServerFixture : TestServerFixture
{
    protected override bool EnforceTimestamps => true;
}

/// <summary>
///     Covers walk timestamp validation: packets that arrive too soon after the
///     previous one are ignored, while a sufficiently advanced timestamp moves.
/// </summary>
public class WalkTimestampTests : IClassFixture<TimestampEnforcingServerFixture>
{
    private const int StartX = 6;
    private const int StartY = 6;

    private readonly TimestampEnforcingServerFixture _fixture;

    public WalkTimestampTests(TimestampEnforcingServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Walk_WithTooSmallTimestampDelta_ShouldBeIgnored()
    {
        await using var client = await LoginAndEnterAsync("walkts");

        // First walk at t=100 is accepted (player starts at t=0).
        var first = await SendWalkAsync(client, Direction.Right, StartX + 1, StartY, timestamp: 100);
        first.Should().BeOfType<WalkReplyServerPacket>();
        _fixture.GetPlayer(client.PlayerId)!.Character!.X.Should().Be(StartX + 1);

        // t=110 is only 10 ahead, less than the required 36, so it is dropped.
        await SendWalkOnlyAsync(client, Direction.Right, StartX + 2, StartY, timestamp: 110);

        // t=150 is 50 ahead of the last accepted timestamp (100). If the 110
        // packet was correctly ignored this is a single step (X = 8); if it had
        // been accepted this would be the second step (X = 9).
        var second = await SendWalkAsync(client, Direction.Right, StartX + 2, StartY, timestamp: 150);
        second.Should().BeOfType<WalkReplyServerPacket>();

        _fixture.GetPlayer(client.PlayerId)!.Character!.X.Should().Be(StartX + 2,
            "the too-soon walk must not have advanced the player");
    }

    // --- Flow helpers ---

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

    private static async Task<IPacket> SendWalkAsync(EoTestClient client, Direction direction, int x, int y,
        int timestamp)
    {
        await SendWalkOnlyAsync(client, direction, x, y, timestamp);
        return await ReceiveUntilAsync(client,
            p => p is WalkReplyServerPacket or WarpRequestServerPacket);
    }

    private static Task SendWalkOnlyAsync(EoTestClient client, Direction direction, int x, int y, int timestamp)
    {
        return client.SendPacketAsync(new WalkPlayerClientPacket
        {
            WalkAction = new WalkAction
            {
                Direction = direction,
                Timestamp = timestamp,
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
