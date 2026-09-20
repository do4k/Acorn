using Acorn.Net;
using Acorn.Tests.Support;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using System.Threading.Tasks;

namespace Acorn.Tests.Integration;

/// <summary>
///     End-to-end coverage for the admin commands added for the warp/who/uptime
///     toolkit. The map warp pair ($wmt/$summon) is driven against a second client
///     so the resulting position changes can be asserted server-side.
/// </summary>
[ClassDataSource<TestServerFixture>(Shared = SharedType.PerClass)]
[NotInParallel("AdminCommandIntegrationTests")]
public class AdminCommandIntegrationTests
{
    private readonly TestServerFixture _fixture;
    private readonly List<EoTestClient> _clients = [];

    public AdminCommandIntegrationTests(TestServerFixture fixture)
    {
        _fixture = fixture;
    }

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
    public async Task Uptime_ShouldReplyWithElapsedTime()
    {
        await WaitForCleanWorldAsync();
        var admin = await LoginAndEnterAsync("cmduptime");
        await PromoteToAdminAsync(admin, AdminLevel.GameMaster);

        await admin.SendPacketAsync(new TalkReportClientPacket { Message = "$uptime" });

        var reply = (TalkMsgServerPacket)await ReceiveUntilAsync(admin, p => p is TalkMsgServerPacket);
        reply.Message.Should().StartWith("Uptime:");
    }

    [Test]
    public async Task Who_ShouldListOnlinePlayers()
    {
        await WaitForCleanWorldAsync();
        var admin = await LoginAndEnterAsync("cmdwho");
        var other = await LoginAndEnterAsync("cmdwhoother");
        await PromoteToAdminAsync(admin, AdminLevel.GameMaster);
        other.StartKeepAlive();

        await admin.SendPacketAsync(new TalkReportClientPacket { Message = "$who" });

        var header = (TalkMsgServerPacket)await ReceiveUntilAsync(admin,
            p => p is TalkMsgServerPacket m && m.Message.StartsWith("Online players"));
        header.Message.Should().Contain("2");

        var listing = (TalkMsgServerPacket)await ReceiveUntilAsync(admin,
            p => p is TalkMsgServerPacket m && m.Message.Contains(CharacterName(other)));
        listing.Message.Should().Contain(CharacterName(other));
    }

    [Test]
    public async Task Repub_ShouldReloadPubFiles()
    {
        await WaitForCleanWorldAsync();
        var admin = await LoginAndEnterAsync("cmdrepub");
        await PromoteToAdminAsync(admin, AdminLevel.GameMaster);

        await admin.SendPacketAsync(new TalkReportClientPacket { Message = "$repub" });

        var reply = (TalkMsgServerPacket)await ReceiveUntilAsync(admin,
            p => p is TalkMsgServerPacket m && m.Message.Contains("Pub files"));
        reply.Message.Should().Contain("reloaded");
    }

    [Test]
    public async Task Wmt_ShouldWarpAdminToTargetPlayer()
    {
        await WaitForCleanWorldAsync();
        var admin = await LoginAndEnterAsync("cmdwmt");
        var target = await LoginAndEnterAsync("cmdwmttarget");
        await PromoteToAdminAsync(admin, AdminLevel.GameMaster);
        target.StartKeepAlive();

        // Move the admin away first so the warp-to-player is observable.
        await admin.SendPacketAsync(new TalkReportClientPacket { Message = "$warp 2" });
        await WaitUntilAsync(() => _fixture.GetPlayer(admin.PlayerId)!.Character!.Map == 2);

        await Task.Delay(600); // Talk/Report is rate limited to 500ms
        await admin.SendPacketAsync(new TalkReportClientPacket { Message = $"$wmt {CharacterName(target)}" });

        var targetCharacter = _fixture.GetPlayer(target.PlayerId)!.Character!;
        await WaitUntilAsync(() =>
        {
            var character = _fixture.GetPlayer(admin.PlayerId)?.Character;
            return character is not null
                   && character.Map == targetCharacter.Map
                   && character.X == targetCharacter.X
                   && character.Y == targetCharacter.Y;
        });

        var adminCharacter = _fixture.GetPlayer(admin.PlayerId)!.Character!;
        adminCharacter.Map.Should().Be(targetCharacter.Map);
        adminCharacter.X.Should().Be(targetCharacter.X);
        adminCharacter.Y.Should().Be(targetCharacter.Y);
    }

    [Test]
    public async Task Summon_ShouldWarpTargetPlayerToAdmin()
    {
        await WaitForCleanWorldAsync();
        var admin = await LoginAndEnterAsync("cmdsummon");
        var target = await LoginAndEnterAsync("cmdsummontarget");
        await PromoteToAdminAsync(admin, AdminLevel.GameMaster);
        target.StartKeepAlive();

        // Move the admin to another map so the summon is observable.
        await admin.SendPacketAsync(new TalkReportClientPacket { Message = "$warp 2" });
        await WaitUntilAsync(() => _fixture.GetPlayer(admin.PlayerId)!.Character!.Map == 2);

        await Task.Delay(600); // Talk/Report is rate limited to 500ms
        await admin.SendPacketAsync(new TalkReportClientPacket { Message = $"$summon {CharacterName(target)}" });

        var adminCharacter = _fixture.GetPlayer(admin.PlayerId)!.Character!;
        await WaitUntilAsync(() =>
        {
            var character = _fixture.GetPlayer(target.PlayerId)?.Character;
            return character is not null
                   && character.Map == adminCharacter.Map
                   && Math.Abs(character.X - adminCharacter.X) <= 3
                   && Math.Abs(character.Y - adminCharacter.Y) <= 3;
        });

        var targetCharacter = _fixture.GetPlayer(target.PlayerId)!.Character!;
        targetCharacter.Map.Should().Be(adminCharacter.Map);
        Math.Abs(targetCharacter.X - adminCharacter.X).Should().BeLessThanOrEqualTo(3);
        Math.Abs(targetCharacter.Y - adminCharacter.Y).Should().BeLessThanOrEqualTo(3);
    }

    // --- Flow helpers (mirrors AdminInteractionTests) ---

    private string CharacterName(EoTestClient client)
    {
        return _fixture.GetPlayer(client.PlayerId)!.Character!.Name!;
    }

    private async Task PromoteToAdminAsync(EoTestClient client, AdminLevel level)
    {
        var player = await WaitForPlayerAsync(client.PlayerId);
        player.Should().NotBeNull();
        player!.Character!.Admin = level;
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

        await client.SendPacketAsync(new WelcomeMsgClientPacket
        {
            SessionId = client.PlayerId,
            CharacterId = client.CharacterId
        });
        var enter = (WelcomeReplyServerPacket)await ReceiveUntilAsync(client, p => p is WelcomeReplyServerPacket);
        enter.WelcomeCode.Should().Be(WelcomeCode.EnterGame);

        return client;
    }

    private static async Task<IPacket> ReceiveUntilAsync(EoTestClient client, Func<IPacket, bool> predicate)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!cts.Token.IsCancellationRequested)
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

    private async Task WaitUntilAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        condition().Should().BeTrue("condition was not met before the timeout");
    }

    private async Task WaitForCleanWorldAsync()
    {
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
