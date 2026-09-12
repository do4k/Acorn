using Acorn.Tests.Support;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using System.Threading.Tasks;

namespace Acorn.Tests.Integration;

/// <summary>
///     End-to-end coverage for the range/visibility handshake: a Range/Request must be
///     answered with a Range/Reply (NearbyInfo) rather than the old Players/List packet.
/// </summary>
[ClassDataSource<TestServerFixture>(Shared = SharedType.PerClass)]
[NotInParallel("RangeVisibilityTests")]
public class RangeVisibilityTests
{
    private readonly TestServerFixture _fixture;

    public RangeVisibilityTests(TestServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Test]
    public async Task RangeRequest_ShouldReplyWithRangeReplyContainingTheRequestedPlayer()
    {
        await using var client = await LoginAndEnterAsync("range");

        await client.SendPacketAsync(new RangeRequestClientPacket
        {
            PlayerIds = [client.PlayerId],
            NpcIndexes = []
        });

        var reply = (RangeReplyServerPacket)await client.ReceivePacketAsync();

        reply.Nearby.Characters.Should().ContainSingle(c => c.PlayerId == client.PlayerId);
    }

    [Test]
    public async Task PlayerRangeRequest_ShouldReplyWithRangeReply()
    {
        await using var client = await LoginAndEnterAsync("prange");

        await client.SendPacketAsync(new PlayerRangeRequestClientPacket
        {
            PlayerIds = [client.PlayerId]
        });

        var reply = (RangeReplyServerPacket)await client.ReceivePacketAsync();

        reply.Nearby.Characters.Should().ContainSingle(c => c.PlayerId == client.PlayerId);
    }

    [Test]
    public async Task NpcRangeRequest_ShouldReplyWithNpcAgree()
    {
        await using var client = await LoginAndEnterAsync("nrange");

        await client.SendPacketAsync(new NpcRangeRequestClientPacket
        {
            NpcIndexes = [0, 1]
        });

        // The generated test map has no NPCs, but the reply must still be Npc/Agree.
        var reply = (NpcAgreeServerPacket)await client.ReceivePacketAsync();

        reply.Npcs.Should().BeEmpty();
    }

    // --- flow helpers ---

    private async Task<EoTestClient> LoginAndEnterAsync(string prefix)
    {
        await WaitForNoPlayersAsync();

        var client = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);
        await client.InitAsync();
        await client.SendConnectionAcceptAsync();

        var username = $"{prefix}{Guid.NewGuid():N}"[..16];
        var password = TestPasswords.Valid;
        var sessionId = await client.AccountRequestAsync(username);
        await client.AccountCreateAsync(username, password, sessionId);
        await client.LoginAsync(username, password);

        var charName = $"rg{Guid.NewGuid():N}"[..10];
        (await client.CreateCharacterAsync(sessionId, charName)).ReplyCode.Should().Be(CharacterReply.Ok);

        await client.SendPacketAsync(new WelcomeRequestClientPacket { CharacterId = client.CharacterId });
        await client.ReceivePacketAsync();

        await client.SendPacketAsync(new WelcomeMsgClientPacket
        {
            SessionId = client.PlayerId,
            CharacterId = client.CharacterId
        });
        var enter = (WelcomeReplyServerPacket)await client.ReceivePacketAsync();
        enter.WelcomeCode.Should().Be(WelcomeCode.EnterGame);

        return client;
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