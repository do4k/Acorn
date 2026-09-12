using Acorn.Tests.Support;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using System.Threading.Tasks;

namespace Acorn.Tests.Integration;

/// <summary>
///     Uses its own server fixture so the database is guaranteed to have no admin
///     characters, making the first-character admin grant deterministic.
/// </summary>
[ClassDataSource<TestServerFixture>(Shared = SharedType.PerClass)]
[NotInParallel("FirstCharacterAdminTests")]
public class FirstCharacterAdminTests
{
    private readonly TestServerFixture _fixture;

    public FirstCharacterAdminTests(TestServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Test]
    public async Task FirstCharacter_WhenNoAdminsExist_ShouldBeHighGameMaster()
    {
        await using var client = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);
        await client.InitAsync();
        await client.SendConnectionAcceptAsync();

        var username = $"first{Guid.NewGuid():N}"[..16];
        var password = TestPasswords.Valid;
        (await client.AccountCreateAsync(username, password, client.PlayerId))
            .Should().Be(AccountReply.Created);
        (await client.LoginAsync(username, password)).ReplyCode.Should().Be(LoginReply.Ok);

        var name = $"admin{Guid.NewGuid():N}"[..10];
        var reply = await client.CreateCharacterAsync(client.PlayerId, name);
        reply.ReplyCode.Should().Be(CharacterReply.Ok);

        var created = ((CharacterReplyServerPacket.ReplyCodeDataOk)reply.ReplyCodeData)
            .Characters.Single(c => c.Name == name);
        created.Admin.Should().Be(AdminLevel.HighGameMaster);
    }
}