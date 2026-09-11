using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Xunit;

namespace Acorn.Tests.Integration;

/// <summary>
///     Single-client end-to-end coverage for the whisper toggle and tell handling
///     introduced for issue #51. Multi-client scenarios are covered by the handler
///     unit tests because the shared test server cannot safely serve two concurrent
///     database-backed sessions.
/// </summary>
public class ChatSocialIntegrationTests : IClassFixture<TestServerFixture>
{
    private readonly TestServerFixture _fixture;

    public ChatSocialIntegrationTests(TestServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GlobalPlayer_DisablesWhispers()
    {
        await using var session = await LoginAndEnterAsync("whispoff");

        await session.Client.SendPacketAsync(new GlobalPlayerClientPacket());
        await WaitUntilAsync(() => !_fixture.GetPlayer(session.Client.PlayerId)!.Whispers);

        _fixture.GetPlayer(session.Client.PlayerId)!.Whispers.Should().BeFalse();
    }

    [Fact]
    public async Task GlobalRemove_EnablesWhispers()
    {
        await using var session = await LoginAndEnterAsync("whispon");

        await session.Client.SendPacketAsync(new GlobalPlayerClientPacket());
        await WaitUntilAsync(() => !_fixture.GetPlayer(session.Client.PlayerId)!.Whispers);

        await session.Client.SendPacketAsync(new GlobalRemoveClientPacket());
        await WaitUntilAsync(() => _fixture.GetPlayer(session.Client.PlayerId)!.Whispers);

        _fixture.GetPlayer(session.Client.PlayerId)!.Whispers.Should().BeTrue();
    }

    [Fact]
    public async Task Tell_WhenWhispersOff_RepliesNotFound()
    {
        await using var session = await LoginAndEnterAsync("telloff");
        await session.Client.SendPacketAsync(new GlobalPlayerClientPacket());
        await WaitUntilAsync(() => !_fixture.GetPlayer(session.Client.PlayerId)!.Whispers);

        await session.Client.SendPacketAsync(new TalkTellClientPacket
        {
            Name = session.CharacterName,
            Message = "hi"
        });

        var reply = await ReceiveUntilAsync(session.Client, p => p is TalkReplyServerPacket) as TalkReplyServerPacket;
        reply.Should().NotBeNull();
        reply!.ReplyCode.Should().Be(TalkReply.NotFound);
    }

    [Fact]
    public async Task Tell_WhenWhispersOn_Delivers()
    {
        await using var session = await LoginAndEnterAsync("tellon");

        await session.Client.SendPacketAsync(new TalkTellClientPacket
        {
            Name = session.CharacterName,
            Message = "hello"
        });

        var tell = await ReceiveUntilAsync(session.Client, p => p is TalkTellServerPacket) as TalkTellServerPacket;
        tell.Should().NotBeNull();
        tell!.Message.Should().Be("hello");
        tell.PlayerName.Should().Be(session.CharacterName);
    }

    [Fact]
    public async Task Tell_WhenMuted_IsIgnored()
    {
        await using var session = await LoginAndEnterAsync("tellmuted");
        _fixture.GetPlayer(session.Client.PlayerId)!.MutedUntil = DateTime.UtcNow.AddMinutes(1);

        await session.Client.SendPacketAsync(new TalkTellClientPacket
        {
            Name = session.CharacterName,
            Message = "hi"
        });

        var received = await TryReceiveMatchingAsync(session.Client, p => p is TalkTellServerPacket or TalkReplyServerPacket,
            600);
        received.Should().BeFalse();
    }

    // --- Flow helpers ---

    private sealed record TestSession(EoTestClient Client, string CharacterName) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Client.DisposeAsync();
    }

    private async Task<TestSession> LoginAndEnterAsync(string prefix)
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

        return new TestSession(client, charName);
    }

    private async Task WaitForNoPlayersAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!cts.IsCancellationRequested && _fixture.OnlinePlayerCount > 0)
        {
            await Task.Delay(25, cts.Token);
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!cts.IsCancellationRequested && !condition())
        {
            await Task.Delay(25, cts.Token);
        }

        condition().Should().BeTrue();
    }

    private static async Task<bool> TryReceiveMatchingAsync(EoTestClient client, Func<IPacket, bool> predicate,
        int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            var packet = await client.TryReceivePacketAsync(remaining);
            if (packet is null)
            {
                break;
            }

            if (packet is ConnectionPlayerServerPacket)
            {
                await client.SendConnectionPingAsync();
                continue;
            }

            if (predicate(packet))
            {
                return true;
            }
        }

        return false;
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
}
