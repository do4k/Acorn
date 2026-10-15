using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Xunit;

namespace Acorn.Tests.Integration;

/// <summary>
/// Integration tests for character creation, listing, and deletion flows.
/// </summary>
public class CharacterFlowTests : IClassFixture<TestServerFixture>
{
    private readonly TestServerFixture _fixture;

    public CharacterFlowTests(TestServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateCharacter_ShouldAppearInLoginCharacterList()
    {
        await using var client = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);
        await client.InitAsync();
        await client.SendConnectionAcceptAsync();

        // Create account and login
        var username = $"chr_{Guid.NewGuid():N}"[..20];
        var password = "testpass123";
        var sessionId = await client.AccountRequestAsync(username);
        await client.AccountCreateAsync(username, password, sessionId);
        var loginReply = await client.LoginAsync(username, password);
        loginReply.ReplyCode.Should().Be(LoginReply.Ok);

        // Login should show 0 characters initially
        var okData = loginReply.ReplyCodeData as LoginReplyServerPacket.ReplyCodeDataOk;
        okData!.Characters.Should().BeEmpty();

        // Request character creation session
        var charSessionId = await client.CharacterRequestAsync();
        charSessionId.Should().BeGreaterThan(0);

        // Create character
        var charName = $"hero{Guid.NewGuid():N}"[..12];
        var createReply = await client.CharacterCreateAsync(charName, charSessionId);
        createReply.Should().Be(CharacterReply.Ok);

        // Dispose original connection so the account is no longer "online"
        await client.DisposeAsync();

        // Re-login on a fresh connection to verify character appears in list
        await using var client2 = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);
        await client2.InitAsync();
        await client2.SendConnectionAcceptAsync();

        var loginReply2 = await client2.LoginAsync(username, password);
        loginReply2.ReplyCode.Should().Be(LoginReply.Ok);

        var okData2 = loginReply2.ReplyCodeData as LoginReplyServerPacket.ReplyCodeDataOk;
        okData2!.Characters.Should().HaveCount(1);
        okData2.Characters[0].Name.Should().Be(charName);
    }

    [Fact]
    public async Task CreateMultipleCharacters_ShouldAllAppearInList()
    {
        await using var client = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);
        await client.InitAsync();
        await client.SendConnectionAcceptAsync();

        var username = $"mul_{Guid.NewGuid():N}"[..20];
        var password = "testpass123";
        var sessionId = await client.AccountRequestAsync(username);
        await client.AccountCreateAsync(username, password, sessionId);
        await client.LoginAsync(username, password);

        // Create 3 characters (the max)
        var names = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var charSessionId = await client.CharacterRequestAsync();
            var name = $"c{i}{Guid.NewGuid():N}"[..12];
            names.Add(name);
            var reply = await client.CharacterCreateAsync(name, charSessionId);
            reply.Should().Be(CharacterReply.Ok);
        }

        // Dispose original connection so the account is no longer "online"
        await client.DisposeAsync();

        // Re-login on a fresh connection and verify all 3 appear
        await using var client2 = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);
        await client2.InitAsync();
        await client2.SendConnectionAcceptAsync();

        var loginReply = await client2.LoginAsync(username, password);
        loginReply.ReplyCode.Should().Be(LoginReply.Ok);
        var okData = loginReply.ReplyCodeData as LoginReplyServerPacket.ReplyCodeDataOk;
        okData!.Characters.Should().HaveCount(3);
    }

    [Fact]
    public async Task CreateDuplicateCharacterName_ShouldReturnExists()
    {
        await using var client = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);
        await client.InitAsync();
        await client.SendConnectionAcceptAsync();

        var username = $"dup_{Guid.NewGuid():N}"[..20];
        var password = "testpass123";
        var sessionId = await client.AccountRequestAsync(username);
        await client.AccountCreateAsync(username, password, sessionId);
        await client.LoginAsync(username, password);

        // Create first character
        var charSessionId = await client.CharacterRequestAsync();
        var name = $"dupe{Guid.NewGuid():N}"[..12];
        var reply1 = await client.CharacterCreateAsync(name, charSessionId);
        reply1.Should().Be(CharacterReply.Ok);

        // Try to create another with the same name
        var charSessionId2 = await client.CharacterRequestAsync();
        var reply2 = await client.CharacterCreateAsync(name, charSessionId2);
        reply2.Should().Be(CharacterReply.Exists);
    }

    [Fact]
    public async Task SelectCharacter_ShouldReturnWelcomeData()
    {
        await using var client = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);
        await client.InitAsync();
        await client.SendConnectionAcceptAsync();

        var username = $"sel_{Guid.NewGuid():N}"[..20];
        var password = "testpass123";
        var sessionId = await client.AccountRequestAsync(username);
        await client.AccountCreateAsync(username, password, sessionId);
        await client.LoginAsync(username, password);

        // Create character
        var charSessionId = await client.CharacterRequestAsync();
        var name = $"sel{Guid.NewGuid():N}"[..12];
        await client.CharacterCreateAsync(name, charSessionId);

        // Dispose original connection so the account is no longer "online"
        await client.DisposeAsync();

        // Re-login on a fresh connection to get the character list
        await using var client2 = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);
        await client2.InitAsync();
        await client2.SendConnectionAcceptAsync();
        await client2.LoginAsync(username, password);

        // Select the character
        var welcomeReply = await client2.WelcomeRequestAsync(0);
        welcomeReply.WelcomeCode.Should().Be(WelcomeCode.SelectCharacter);

        var selectData = welcomeReply.WelcomeCodeData as WelcomeReplyServerPacket.WelcomeCodeDataSelectCharacter;
        selectData.Should().NotBeNull();
        selectData!.Name.Should().Be(name);
        selectData.SessionId.Should().BeGreaterThan(0);
    }
}
