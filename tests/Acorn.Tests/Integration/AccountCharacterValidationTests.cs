using Acorn.Tests.Support;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using System.Threading.Tasks;

namespace Acorn.Tests.Integration;

/// <summary>
///     Integration tests for account/character validation rules: username and
///     password length, name validity, lowercasing, slot limits, appearance
///     ranges and database-backed character ids.
/// </summary>
[ClassDataSource<TestServerFixture>(Shared = SharedType.PerClass)]
[NotInParallel("AccountCharacterValidationTests")]
public class AccountCharacterValidationTests
{
    private readonly TestServerFixture _fixture;

    public AccountCharacterValidationTests(TestServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Test]
    public async Task AccountCreate_WithInvalidUsername_ShouldReturnNotApproved()
    {
        await using var client = await ConnectAsync();

        var reply = await client.AccountCreateAsync("Bad_User!", TestPasswords.Valid, client.PlayerId);

        reply.Should().Be(AccountReply.NotApproved);
    }

    [Test]
    public async Task AccountCreate_WithTooShortPassword_ShouldReturnNotApproved()
    {
        await using var client = await ConnectAsync();
        var username = $"short{Guid.NewGuid():N}"[..16];

        var reply = await client.AccountCreateAsync(username, "abc", client.PlayerId);

        reply.Should().Be(AccountReply.NotApproved);
    }

    [Test]
    public async Task AccountCreate_WithTooLongPassword_ShouldReturnNotApproved()
    {
        await using var client = await ConnectAsync();
        var username = $"long{Guid.NewGuid():N}"[..16];

        var reply = await client.AccountCreateAsync(username, new string('x', 30), client.PlayerId);

        reply.Should().Be(AccountReply.NotApproved);
    }

    [Test]
    public async Task AccountCreate_WithUppercaseUsername_ShouldNormalizeAndAllowLogin()
    {
        await using var client = await ConnectAsync();
        var username = $"upper{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        var password = TestPasswords.Valid;

        var createReply = await client.AccountCreateAsync(username, password, client.PlayerId);
        createReply.Should().Be(AccountReply.Created);

        // Logging in with the original (mixed/upper case) username should still work.
        var loginReply = await client.LoginAsync(username, password);
        loginReply.ReplyCode.Should().Be(LoginReply.Ok);
    }

    [Test]
    public async Task CreateCharacter_WithInvalidName_ShouldReturnNotApproved()
    {
        await using var client = await CreateAndLoginAsync("badname");

        var reply = await client.CreateCharacterAsync(client.PlayerId, "X");

        reply.ReplyCode.Should().Be(CharacterReply.NotApproved);
    }

    [Test]
    public async Task CreateCharacter_WithReservedName_ShouldReturnNotApproved()
    {
        await using var client = await CreateAndLoginAsync("reserved");

        var reply = await client.CreateCharacterAsync(client.PlayerId, "server");

        reply.ReplyCode.Should().Be(CharacterReply.NotApproved);
    }

    [Test]
    public async Task CreateCharacter_WithInvalidAppearance_ShouldReturnNotApproved()
    {
        await using var client = await CreateAndLoginAsync("appearance");
        var name = $"badapp{Guid.NewGuid():N}"[..12];

        var reply = await client.CreateCharacterAsync(client.PlayerId, name, hairStyle: 99);

        reply.ReplyCode.Should().Be(CharacterReply.NotApproved);
    }

    [Test]
    public async Task CreateCharacter_WhenAccountFull_ShouldReturnFull()
    {
        await using var client = await CreateAndLoginAsync("full");

        for (var i = 0; i < 3; i++)
        {
            var name = $"full{i}{Guid.NewGuid():N}"[..12];
            var reply = await client.CreateCharacterAsync(client.PlayerId, name);
            reply.ReplyCode.Should().Be(CharacterReply.Ok);
        }

        var fourth = await client.CreateCharacterAsync(client.PlayerId, $"x{Guid.NewGuid():N}"[..8]);
        fourth.ReplyCode.Should().Be(CharacterReply.Full);
    }

    [Test]
    public async Task CharacterTake_ShouldEchoDatabaseCharacterId()
    {
        await using var client = await CreateAndLoginAsync("take");
        var name = $"take{Guid.NewGuid():N}"[..10];

        var createReply = await client.CreateCharacterAsync(client.PlayerId, name);
        createReply.ReplyCode.Should().Be(CharacterReply.Ok);

        var characterId = client.CharacterId;
        characterId.Should().BeGreaterThan(0);

        await client.SendPacketAsync(new CharacterTakeClientPacket { CharacterId = characterId });
        var response = await client.ReceivePacketAsync();

        var player = response.Should().BeOfType<CharacterPlayerServerPacket>().Subject;
        player.CharacterId.Should().Be(characterId);
        player.SessionId.Should().Be(client.PlayerId);
    }

    private async Task<EoTestClient> ConnectAsync()
    {
        var client = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);
        await client.InitAsync();
        await client.SendConnectionAcceptAsync();
        return client;
    }

    private async Task<EoTestClient> CreateAndLoginAsync(string prefix)
    {
        var client = await ConnectAsync();
        var username = $"{prefix}{Guid.NewGuid():N}"[..16];
        var password = TestPasswords.Valid;

        (await client.AccountCreateAsync(username, password, client.PlayerId))
            .Should().Be(AccountReply.Created);
        (await client.LoginAsync(username, password)).ReplyCode.Should().Be(LoginReply.Ok);

        return client;
    }
}

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