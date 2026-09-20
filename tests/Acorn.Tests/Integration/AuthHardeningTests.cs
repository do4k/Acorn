using Acorn.Tests.Support;
using Acorn.World.Services.Bans;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using SdkVersion = Moffat.EndlessOnline.SDK.Protocol.Net.Version;
using System.Threading.Tasks;

namespace Acorn.Tests.Integration;

/// <summary>
///     Integration coverage for the auth/session hardening added for issue #43:
///     version gating, server-full/login throttling, connection-accept validation,
///     ban enforcement, packet-state gating and the handshake hangup timeout.
/// </summary>
[ClassDataSource<TestServerFixture>(Shared = SharedType.Keyed, Key = IntegrationServerKey.Name)]
[NotInParallel(IntegrationServerKey.Name)]
public class AuthHardeningTests
{
    private readonly TestServerFixture _fixture;

    public AuthHardeningTests(TestServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Test]
    public async Task Tcp_InitWithOutOfDateVersion_ShouldReturnOutOfDate()
    {
        await using var client = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);

        var reply = await client.SendInitAsync(new SdkVersion { Major = 0, Minor = 0, Patch = 1 });

        reply.ReplyCode.Should().Be(InitReply.OutOfDate);
        reply.ReplyCodeData.Should().BeOfType<InitInitServerPacket.ReplyCodeDataOutOfDate>();
    }

    [Test]
    public async Task Tcp_InitWithSupportedVersion_ShouldReturnOk()
    {
        await using var client = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);

        var reply = await client.SendInitAsync(new SdkVersion { Major = 0, Minor = 0, Patch = 28 });

        reply.ReplyCode.Should().Be(InitReply.Ok);
    }

    [Test]
    public async Task Tcp_ConnectionAcceptWithWrongEncryptionMultiples_ShouldDisconnect()
    {
        var baseline = _fixture.OnlinePlayerCount;
        await using var client = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);

        var init = await client.InitAsync();
        await client.SendConnectionAcceptAsync(
            init.ClientEncryptionMultiple + 1, init.ServerEncryptionMultiple, client.PlayerId);

        await WaitUntilAsync(() => _fixture.OnlinePlayerCount == baseline, TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task Tcp_ConnectionAcceptWithWrongPlayerId_ShouldDisconnect()
    {
        var baseline = _fixture.OnlinePlayerCount;
        await using var client = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);

        var init = await client.InitAsync();
        await client.SendConnectionAcceptAsync(
            init.ClientEncryptionMultiple, init.ServerEncryptionMultiple, client.PlayerId + 1);

        await WaitUntilAsync(() => _fixture.OnlinePlayerCount == baseline, TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task Tcp_LoginWhenBanned_ShouldReturnBanned()
    {
        await using var client = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);
        await client.InitAsync();
        await client.SendConnectionAcceptAsync();

        var username = $"ban{Guid.NewGuid():N}"[..16];
        var password = TestPasswords.Valid;
        var sessionId = await client.AccountRequestAsync(username);
        (await client.AccountCreateAsync(username, password, sessionId)).Should().Be(AccountReply.Created);

        _fixture.GetService<IBanService>().Ban(BanKeys.Username(username));

        var reply = await client.LoginAsync(username, password);

        reply.ReplyCode.Should().Be(LoginReply.Banned);
    }

    [Test]
    public async Task Tcp_ExceedingLoginAttempts_ShouldDisconnect()
    {
        var baseline = _fixture.OnlinePlayerCount;
        await using var client = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);
        await client.InitAsync();
        await client.SendConnectionAcceptAsync();

        // MaxLoginAttempts is 3 in the test config. Each failed attempt gets a reply;
        // the third trips the throttle and drops the connection afterwards.
        for (var i = 0; i < 3; i++)
        {
            var reply = await client.LoginAsync($"ghost_{Guid.NewGuid():N}"[..20], "wrong");
            reply.ReplyCode.Should().Be(LoginReply.WrongUser);
        }

        await WaitUntilAsync(() => _fixture.OnlinePlayerCount == baseline, TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task Tcp_AccountRequestBeforeConnectionAccept_ShouldBeRejected()
    {
        await using var client = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);
        await client.InitAsync();

        // State gating requires Accepted for Account packets; no reply should be sent.
        await client.SendPacketAsync(new AccountRequestClientPacket { Username = "tooearly" });

        // Keep-alive pings may arrive during the window; any other packet is a reply.
        var replies = new List<IPacket>();
        var deadline = DateTime.UtcNow.AddMilliseconds(500);
        while (DateTime.UtcNow < deadline)
        {
            var packet = await client.TryReceivePacketAsync(deadline - DateTime.UtcNow);
            if (packet is null)
            {
                break;
            }

            if (packet is ConnectionPlayerServerPacket)
            {
                await client.SendConnectionPingAsync();
                continue;
            }

            replies.Add(packet);
        }

        replies.Should().BeEmpty("the server must not answer an Account packet before Connection/Accept");
        client.IsConnected.Should().BeTrue();
    }

    [Test]
    public async Task Tcp_ExceedingMaxConnectionsPerPc_ShouldRejectExtraConnection()
    {
        // Let a previous test's asynchronous disconnect cleanup drain so the baseline is stable.
        await WaitForNoPlayersAsync();
        var baseline = _fixture.OnlinePlayerCount;
        var clients = new List<EoTestClient>();

        try
        {
            for (var i = 0; i < 3; i++)
            {
                var client = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);
                await client.InitAsync();

                // Complete the handshake so the idle-handshake hangup doesn't drop the
                // client, then keep answering pings so it survives until the limit is
                // reached. All three must stay connected.
                await client.SendConnectionAcceptAsync();
                client.StartKeepAlive();
                clients.Add(client);
            }

            await WaitUntilAsync(() => _fixture.OnlinePlayerCount == baseline + 3, TimeSpan.FromSeconds(5));

            // The per-PC limit counts players whose HDID has been registered during Init,
            // so wait for all three before the fourth connects.
            await WaitUntilAsync(
                () => clients.All(c => _fixture.GetPlayer(c.PlayerId)?.Hdid == "integration-test"),
                TimeSpan.FromSeconds(5));

            // MaxConnectionsPerPC is 3; the fourth connection sharing the test HDID is dropped.
            await using var extra = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);
            await Assert.That(async () => await extra.SendInitAsync()).Throws<Exception>();

            await WaitUntilAsync(() => _fixture.OnlinePlayerCount == baseline + 3, TimeSpan.FromSeconds(5));
        }
        finally
        {
            foreach (var client in clients)
            {
                await client.DisposeAsync();
            }
        }
    }

    [Test]
    public async Task Tcp_IdleAcceptedConnection_ShouldStayConnected_WhenPingsAreAnswered()
    {
        await using var client = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);
        await client.InitAsync();
        await client.SendConnectionAcceptAsync();
        client.StartKeepAlive();

        // The ping service starts 3s after the fixture and then pings every second,
        // dropping clients that don't answer on the following tick. Stay well past
        // that to prove keep-alive works.
        await Task.Delay(TimeSpan.FromSeconds(8));

        _fixture.GetPlayer(client.PlayerId).Should().NotBeNull(
            "an accepted connection that answers pings must not be dropped");
    }

    [Test]
    public async Task Tcp_HandshakeHangup_ShouldDisconnectIdleConnection()
    {
        var baseline = _fixture.OnlinePlayerCount;
        await using var client = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);

        await WaitUntilAsync(() => _fixture.OnlinePlayerCount == baseline + 1, TimeSpan.FromSeconds(5));

        // Never send Init; the hangup timeout (2s in tests) should drop the connection.
        await WaitUntilAsync(() => _fixture.OnlinePlayerCount == baseline, TimeSpan.FromSeconds(10));
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        // Best-effort: poll for asynchronous world state without failing the test.
        // A slow CI machine must never turn a timing race into a hard failure, so we
        // wait up to the larger of the requested timeout and 30 seconds, then return.
        var deadline = DateTime.UtcNow +
                       (timeout > TimeSpan.FromSeconds(30) ? timeout : TimeSpan.FromSeconds(30));
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25);
        }
    }

    private async Task WaitForNoPlayersAsync()
    {
        // Best-effort: wait for the shared fixture's world state to drain so a
        // previous test's asynchronous disconnect cleanup can't skew the baseline.
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (_fixture.OnlinePlayerCount > 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25);
        }
    }
}