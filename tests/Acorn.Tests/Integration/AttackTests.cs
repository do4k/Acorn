using Acorn.Tests.Support;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Xunit;

namespace Acorn.Tests.Integration;

/// <summary>
///     Covers the attack packet flow: pre-attack validation and the direction the
///     server uses for the attack animation broadcast.
/// </summary>
public class AttackTests : IClassFixture<TestServerFixture>
{
    private const int StartX = 6;
    private const int StartY = 6;

    private readonly TestServerFixture _fixture;

    public AttackTests(TestServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Attack_WhenSitting_ShouldBeIgnored()
    {
        await WaitForNoPlayersAsync();
        await using var client = await LoginAndEnterAsync("atksit");

        await client.SendPacketAsync(new SitRequestClientPacket
        {
            SitAction = SitAction.Sit,
            SitActionData = new SitRequestClientPacket.SitActionDataSit
            {
                CursorCoords = new Coords { X = StartX, Y = StartY }
            }
        });
        await ReceiveUntilAsync(client, p => p is SitReplyServerPacket);

        await client.SendPacketAsync(new AttackUseClientPacket
        {
            Direction = Direction.Left,
            Timestamp = 0
        });

        // Standing up later guarantees the attack packet has already been processed
        // (TCP ordering), and the sit/stand handler is rate limited to 500ms.
        await Task.Delay(600);
        await client.SendPacketAsync(new SitRequestClientPacket { SitAction = SitAction.Stand });
        await ReceiveUntilAsync(client, p => p is SitCloseServerPacket);

        var player = _fixture.GetPlayer(client.PlayerId)!;
        player.LastAttackTime.Should().Be(default(DateTime),
            "a sitting player's attack must be rejected before the cooldown is consumed");
    }

    [Fact]
    public async Task Attack_ShouldUsePacketDirection_AndBroadcastToInRangePlayers()
    {
        await WaitForNoPlayersAsync();
        await using var attacker = await LoginAndEnterAsync("atkdir");
        await using var observer = await LoginAndEnterAsync("atkobs");

        // The characters spawn facing Down; the attack packet asks for Left, so the
        // broadcast direction proves the packet direction is used.
        _fixture.GetPlayer(attacker.PlayerId)!.Character!.Direction.Should().NotBe(Direction.Left);

        await attacker.SendPacketAsync(new AttackUseClientPacket
        {
            Direction = Direction.Left,
            Timestamp = 0
        });

        // Both players start on the same tile, so the observer is in client range.
        var attack = await ReceiveUntilAsync(observer,
            p => p is AttackPlayerServerPacket a && a.PlayerId == attacker.PlayerId) as AttackPlayerServerPacket;

        attack.Should().NotBeNull();
        attack!.Direction.Should().Be(Direction.Left,
            "the server must use the direction from the attack packet, not the character's facing");
    }

    // --- Flow helpers ---

    private async Task<EoTestClient> LoginAndEnterAsync(string prefix)
    {
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
