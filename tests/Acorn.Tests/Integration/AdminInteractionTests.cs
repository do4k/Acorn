using Acorn.Tests.Support;
using Acorn.Net;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using System.Threading.Tasks;

namespace Acorn.Tests.Integration;

/// <summary>
///     Covers AdminInteract routing: help requests (Tell) and player reports (Report)
///     must reach online admins as AdminInteract/Reply packets, and admin hide/unhide
///     must use AdminInteract/Remove and AdminInteract/Agree instead of Players packets.
/// </summary>
[ClassDataSource<TestServerFixture>(Shared = SharedType.PerClass)]
[NotInParallel("AdminInteractionTests")]
public class AdminInteractionTests
{
    private const int AdminBoardId = 7;
    private readonly TestServerFixture _fixture;
    private readonly List<EoTestClient> _clients = [];

    public AdminInteractionTests(TestServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Before(Test)]
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Disposes test clients one at a time. Disconnecting two clients at once races on
    ///     the shared (captive) DbContext used by disconnect cleanup, so serializing the
    ///     disconnects keeps world state consistent between tests.
    /// </summary>
    [After(Test)]
    public async Task DisposeAsync()
    {
        foreach (var client in _clients)
        {
            var sessionId = client.PlayerId;
            await client.DisposeAsync();
            await WaitForPlayerGoneAsync(sessionId);
        }

        _clients.Clear();
    }

    [Test]
    public async Task HelpRequest_FromPlayer_ShouldBeDeliveredToAdminsAsReplyMessage()
    {
        await WaitForCleanWorldAsync();
        var admin = await LoginAndEnterAsync("adminhelp");
        var helper = await LoginAndEnterAsync("helper");

        await PromoteToAdminAsync(admin, AdminLevel.GameMaster);
        var helperName = CharacterName(helper);

        await helper.SendPacketAsync(new AdminInteractTellClientPacket { Message = "please help me" });

        var reply = await ReceiveUntilAsync(admin, p => p is AdminInteractReplyServerPacket)
            as AdminInteractReplyServerPacket;

        reply.Should().NotBeNull();
        reply!.MessageType.Should().Be(AdminMessageType.Message);
        var data = reply.MessageTypeData.Should()
            .BeOfType<AdminInteractReplyServerPacket.MessageTypeDataMessage>().Subject;
        data.PlayerName.Should().Be(helperName);
        data.Message.Should().Be("please help me");

        var confirmation = await ReceiveUntilAsync(helper, p => p is TalkMsgServerPacket)
            as TalkMsgServerPacket;
        confirmation.Should().NotBeNull();
        confirmation!.Message.Should().Contain("help request");
    }

    [Test]
    public async Task HelpRequest_ShouldNotRequireSenderToBeAnAdmin()
    {
        await WaitForCleanWorldAsync();
        var admin = await LoginAndEnterAsync("adminhelp2");
        var helper = await LoginAndEnterAsync("helper2");

        await PromoteToAdminAsync(admin, AdminLevel.GameMaster);
        _fixture.GetPlayer(helper.PlayerId)!.Character!.Admin.Should().Be(AdminLevel.Player);

        await helper.SendPacketAsync(new AdminInteractTellClientPacket { Message = "are you there?" });

        var reply = await ReceiveUntilAsync(admin, p => p is AdminInteractReplyServerPacket)
            as AdminInteractReplyServerPacket;

        reply.Should().NotBeNull();
        reply!.MessageType.Should().Be(AdminMessageType.Message);
    }

    [Test]
    public async Task Report_FromPlayer_ShouldBeDeliveredToAdminsAndPersisted()
    {
        await WaitForCleanWorldAsync();
        var admin = await LoginAndEnterAsync("adminreport");
        var reporter = await LoginAndEnterAsync("reporter");

        await PromoteToAdminAsync(admin, AdminLevel.GameMaster);
        var reporterName = CharacterName(reporter);
        var reportee = "BadActor";

        await reporter.SendPacketAsync(new AdminInteractReportClientPacket
        {
            Reportee = reportee,
            Message = "using forbidden magic"
        });

        var reply = await ReceiveUntilAsync(admin, p => p is AdminInteractReplyServerPacket)
            as AdminInteractReplyServerPacket;

        reply.Should().NotBeNull();
        reply!.MessageType.Should().Be(AdminMessageType.Report);
        var data = reply.MessageTypeData.Should()
            .BeOfType<AdminInteractReplyServerPacket.MessageTypeDataReport>().Subject;
        data.PlayerName.Should().Be(reporterName);
        data.ReporteeName.Should().Be(reportee);
        data.Message.Should().Be("using forbidden magic");

        var confirmation = await ReceiveUntilAsync(reporter, p => p is TalkMsgServerPacket)
            as TalkMsgServerPacket;
        confirmation.Should().NotBeNull();
        confirmation!.Message.Should().Contain(reportee);

        var posts = await _fixture.GetBoardPostsAsync(AdminBoardId);
        posts.Should().Contain(p =>
            p.BoardId == AdminBoardId &&
            p.CharacterName == reporterName &&
            p.Subject.Contains(reportee) &&
            p.Body == "using forbidden magic");
    }

    [Test]
    public async Task AdminHide_ShouldSendAdminInteractRemoveToMap()
    {
        await WaitForCleanWorldAsync();
        var admin = await LoginAndEnterAsync("adminhide");
        var observer = await LoginAndEnterAsync("observer");

        await PromoteToAdminAsync(admin, AdminLevel.GameMaster);

        await admin.SendPacketAsync(new TalkReportClientPacket { Message = "$hide" });

        // The admin is on the map too, so drain its own copy first (also keeps it alive).
        await ReceiveUntilAsync(admin, p => p is AdminInteractRemoveServerPacket);
        var remove = await ReceiveUntilAsync(observer, p => p is AdminInteractRemoveServerPacket)
            as AdminInteractRemoveServerPacket;

        remove.Should().NotBeNull();
        remove!.PlayerId.Should().Be(admin.PlayerId);
        _fixture.GetPlayer(admin.PlayerId)!.Character!.Hidden.Should().BeTrue();
    }

    [Test]
    public async Task AdminUnhide_ShouldSendAdminInteractAgreeToMap()
    {
        await WaitForCleanWorldAsync();
        var admin = await LoginAndEnterAsync("adminunhide");
        var observer = await LoginAndEnterAsync("observer2");

        await PromoteToAdminAsync(admin, AdminLevel.GameMaster);

        await admin.SendPacketAsync(new TalkReportClientPacket { Message = "$hide" });
        await ReceiveUntilAsync(admin, p => p is TalkMsgServerPacket);
        await ReceiveUntilAsync(observer, p => p is AdminInteractRemoveServerPacket);

        // Talk/Report is rate limited to 500ms; wait before sending the next command.
        await Task.Delay(600);

        await admin.SendPacketAsync(new TalkReportClientPacket { Message = "$show" });

        await ReceiveUntilAsync(admin, p => p is TalkMsgServerPacket);
        var agree = await ReceiveUntilAsync(observer, p => p is AdminInteractAgreeServerPacket)
            as AdminInteractAgreeServerPacket;

        agree.Should().NotBeNull();
        agree!.PlayerId.Should().Be(admin.PlayerId);
        _fixture.GetPlayer(admin.PlayerId)!.Character!.Hidden.Should().BeFalse();
    }

    // --- Flow helpers ---

    private string CharacterName(EoTestClient client)
    {
        return _fixture.GetPlayer(client.PlayerId)!.Character!.Name!;
    }

    private async Task PromoteToAdminAsync(EoTestClient client, AdminLevel level)
    {
        var player = await WaitForPlayerAsync(client.PlayerId);
        player.Should().NotBeNull();
        player!.Character.Should().NotBeNull();
        player.Character!.Admin = level;
    }

    private async Task<PlayerState?> WaitForPlayerAsync(int sessionId)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!cts.IsCancellationRequested)
        {
            var player = _fixture.GetPlayer(sessionId);
            if (player?.Character is not null)
            {
                return player;
            }

            await Task.Delay(25, cts.Token);
        }

        return _fixture.GetPlayer(sessionId);
    }

    private async Task<EoTestClient> LoginAndEnterAsync(string prefix)
    {
        var client = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);
        _clients.Add(client);

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
        var welcome = (WelcomeReplyServerPacket)await ReceiveUntilAsync(client, p => p is WelcomeReplyServerPacket);
        welcome.WelcomeCode.Should().Be(WelcomeCode.SelectCharacter);

        await client.SendPacketAsync(new WelcomeMsgClientPacket { SessionId = client.PlayerId, CharacterId = client.CharacterId });
        var enter = (WelcomeReplyServerPacket)await ReceiveUntilAsync(client, p => p is WelcomeReplyServerPacket);
        enter.WelcomeCode.Should().Be(WelcomeCode.EnterGame);

        return client;
    }

    private static async Task<IPacket> ReceiveUntilAsync(EoTestClient client, Func<IPacket, bool> predicate)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!cts.IsCancellationRequested)
        {
            var packet = await client.ReceivePacketAsync();

            // Keep the connection alive so the ping service doesn't disconnect us mid-test.
            if (packet is ConnectionPlayerServerPacket)
            {
                await client.SendConnectionPingAsync();
            }

            if (predicate(packet))
            {
                return packet;
            }
        }

        throw new TimeoutException("Did not receive expected packet within the timeout");
    }

    private async Task WaitForCleanWorldAsync()
    {
        // Best-effort: disconnect cleanup is asynchronous, so give the world a moment to
        // drain before connecting. Never fail the test if a prior session is still being
        // torn down - each connection now has its own DI scope/DbContext (#76).
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (_fixture.OnlinePlayerCount > 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25);
        }
    }

    private async Task WaitForPlayerGoneAsync(int sessionId)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!cts.IsCancellationRequested && _fixture.GetPlayer(sessionId) is not null)
        {
            await Task.Delay(25, cts.Token);
        }
    }
}