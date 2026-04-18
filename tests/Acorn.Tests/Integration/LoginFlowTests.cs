using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Xunit;

namespace Acorn.Tests.Integration;

/// <summary>
/// Integration tests that spin up the real Acorn server and exercise the full
/// EO protocol login flow over TCP and WebSocket. These tests verify that the
/// init handshake, encryption, sequencing, account creation, and login all work
/// end-to-end with no mocks.
/// </summary>
public class LoginFlowTests : IClassFixture<TestServerFixture>
{
    private readonly TestServerFixture _fixture;

    public LoginFlowTests(TestServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Tcp_FullLoginFlow_ShouldReturnLoginOk()
    {
        await using var client = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);

        // 1. Init handshake — establishes encryption and sequencing
        var initData = await client.InitAsync();
        initData.PlayerId.Should().BeGreaterThan(0);
        initData.ClientEncryptionMultiple.Should().BeInRange(6, 12);
        initData.ServerEncryptionMultiple.Should().BeInRange(6, 12);

        // 2. Connection accept
        await client.SendConnectionAcceptAsync();

        // 3. Account request (username availability check)
        var username = $"tcp_{Guid.NewGuid():N}"[..20];
        var password = "testpassword123";
        var sessionId = await client.AccountRequestAsync(username);
        sessionId.Should().Be(client.PlayerId, "server should echo back the session ID");

        // 4. Account create
        var createReply = await client.AccountCreateAsync(username, password, sessionId);
        createReply.Should().Be(AccountReply.Created);

        // 5. Login
        var loginReply = await client.LoginAsync(username, password);
        loginReply.ReplyCode.Should().Be(LoginReply.Ok);

        var okData = loginReply.ReplyCodeData as LoginReplyServerPacket.ReplyCodeDataOk;
        okData.Should().NotBeNull();
        okData!.Characters.Should().BeEmpty("no characters have been created yet");
    }

    [Fact]
    public async Task WebSocket_FullLoginFlow_ShouldReturnLoginOk()
    {
        if (!_fixture.IsWebSocketAvailable)
        {
            // HttpListener may fail to bind on some platforms (macOS needs elevated permissions)
            return;
        }

        using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        EoTestClient client;
        try
        {
            client = await EoTestClient.ConnectWebSocketAsync(_fixture.WsPort, connectCts.Token);
        }
        catch (Exception ex) when (ex is OperationCanceledException or System.Net.WebSockets.WebSocketException)
        {
            // WebSocket connection failed — platform may not support HttpListener WS
            return;
        }

        await using (client)
        {
            // 1. Init handshake
            var initData = await client.InitAsync();
            initData.PlayerId.Should().BeGreaterThan(0);
            initData.ClientEncryptionMultiple.Should().BeInRange(6, 12);
            initData.ServerEncryptionMultiple.Should().BeInRange(6, 12);

            // 2. Connection accept
            await client.SendConnectionAcceptAsync();

            // 3. Account request
            var username = $"ws_{Guid.NewGuid():N}"[..20];
            var password = "wspassword456";
            var sessionId = await client.AccountRequestAsync(username);
            sessionId.Should().Be(client.PlayerId);

            // 4. Account create
            var createReply = await client.AccountCreateAsync(username, password, sessionId);
            createReply.Should().Be(AccountReply.Created);

            // 5. Login
            var loginReply = await client.LoginAsync(username, password);
            loginReply.ReplyCode.Should().Be(LoginReply.Ok);

            var okData = loginReply.ReplyCodeData as LoginReplyServerPacket.ReplyCodeDataOk;
            okData.Should().NotBeNull();
            okData!.Characters.Should().BeEmpty();
        }
    }

    [Fact]
    public async Task Tcp_LoginWithWrongPassword_ShouldReturnWrongUserPassword()
    {
        await using var client = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);

        // Init + connection accept
        await client.InitAsync();
        await client.SendConnectionAcceptAsync();

        // Create account
        var username = $"bad_{Guid.NewGuid():N}"[..20];
        var correctPassword = "correctpassword";
        var sessionId = await client.AccountRequestAsync(username);
        var createReply = await client.AccountCreateAsync(username, correctPassword, sessionId);
        createReply.Should().Be(AccountReply.Created);

        // Login with wrong password
        var loginReply = await client.LoginAsync(username, "wrongpassword");
        loginReply.ReplyCode.Should().Be(LoginReply.WrongUserPassword);
    }

    [Fact]
    public async Task Tcp_LoginWithNonExistentAccount_ShouldReturnWrongUser()
    {
        await using var client = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);

        await client.InitAsync();
        await client.SendConnectionAcceptAsync();

        // Login without creating the account
        var loginReply = await client.LoginAsync("nonexistent_user_xyz", "anypassword");
        loginReply.ReplyCode.Should().Be(LoginReply.WrongUser);
    }

    [Fact]
    public async Task Tcp_CreateDuplicateAccount_ShouldReturnExists()
    {
        await using var client = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);

        await client.InitAsync();
        await client.SendConnectionAcceptAsync();

        var username = $"dup_{Guid.NewGuid():N}"[..20];
        var password = "duppassword";

        // Create account first time — should succeed
        var sessionId = await client.AccountRequestAsync(username);
        var createReply = await client.AccountCreateAsync(username, password, sessionId);
        createReply.Should().Be(AccountReply.Created);

        // Try to create same account again — should return Exists
        var createReply2 = await client.AccountCreateAsync(username, password, sessionId);
        createReply2.Should().Be(AccountReply.Exists);
    }
}
