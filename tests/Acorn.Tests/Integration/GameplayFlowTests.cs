using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net;
using Moffat.EndlessOnline.SDK.Protocol.Net.Server;
using Xunit;

namespace Acorn.Tests.Integration;

/// <summary>
/// Integration tests for entering the game world and performing basic actions.
/// These tests exercise the full flow: account → character → world entry → gameplay.
/// </summary>
public class GameplayFlowTests : IClassFixture<TestServerFixture>
{
    private readonly TestServerFixture _fixture;

    public GameplayFlowTests(TestServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task EnterWorld_ShouldReturnEnterGameData()
    {
        await using var client = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);
        await client.InitAsync();
        await client.SendConnectionAcceptAsync();

        var username = $"ew_{Guid.NewGuid():N}"[..20];
        var password = "testpass123";
        var sessionId = await client.AccountRequestAsync(username);
        await client.AccountCreateAsync(username, password, sessionId);
        await client.LoginAsync(username, password);

        // Create character
        var charSessionId = await client.CharacterRequestAsync();
        var name = $"ew{Guid.NewGuid():N}"[..12];
        await client.CharacterCreateAsync(name, charSessionId);

        // Re-login and select character
        await client.LoginAsync(username, password);
        var welcomeReply = await client.WelcomeRequestAsync(0);
        welcomeReply.WelcomeCode.Should().Be(WelcomeCode.SelectCharacter);

        var selectData = welcomeReply.WelcomeCodeData as WelcomeReplyServerPacket.WelcomeCodeDataSelectCharacter;
        selectData.Should().NotBeNull();

        // Enter the world
        var enterReply = await client.WelcomeMsgAsync(selectData!.SessionId);
        enterReply.WelcomeCode.Should().Be(WelcomeCode.EnterGame);

        var enterData = enterReply.WelcomeCodeData as WelcomeReplyServerPacket.WelcomeCodeDataEnterGame;
        enterData.Should().NotBeNull();
        enterData!.Items.Should().NotBeNull();
        enterData.Nearby.Should().NotBeNull();
    }

    [Fact]
    public async Task Walk_AfterEnteringWorld_ShouldNotDisconnect()
    {
        await using var client = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);
        await client.InitAsync();
        await client.SendConnectionAcceptAsync();
        await client.CreateAccountAndEnterWorldAsync();

        // Walk south — server should process without disconnecting.
        // The new character spawns at (6,6) per test config.
        await client.SendWalkAsync(Direction.Down, 6, 7);

        // If we can still send and the connection is alive, the walk succeeded.
        // Send another action to verify connectivity.
        await client.SendFaceAsync(Direction.Left);

        // Give server time to process, then verify connection is still alive
        // by sending a chat message
        await Task.Delay(200);
        await client.SendChatAsync("hello world");

        // If we get here without an exception, the connection is stable
    }

    [Fact]
    public async Task Face_AfterEnteringWorld_ShouldReceiveFaceResponse()
    {
        await using var client = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);
        await client.InitAsync();
        await client.SendConnectionAcceptAsync();
        await client.CreateAccountAndEnterWorldAsync();

        // Face a direction
        await client.SendFaceAsync(Direction.Left);

        // Server broadcasts FacePlayerServerPacket to all players on the map
        // Since we're the only player, we should receive our own face update
        var response = await client.ReceiveExpectedAsync<FacePlayerServerPacket>();
        response.Should().NotBeNull();
        response.Direction.Should().Be(Direction.Left);
    }

    [Fact]
    public async Task Chat_AfterEnteringWorld_ShouldBeBroadcastToOtherPlayers()
    {
        // Chat is broadcast to all players on the map EXCEPT the sender,
        // so we need two clients to verify the broadcast.
        await using var sender = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);
        await sender.InitAsync();
        await sender.SendConnectionAcceptAsync();
        await sender.CreateAccountAndEnterWorldAsync();

        await using var receiver = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);
        await receiver.InitAsync();
        await receiver.SendConnectionAcceptAsync();
        await receiver.CreateAccountAndEnterWorldAsync();

        // Sender sends a chat message
        await sender.SendChatAsync("integration test message");

        // Receiver should get the broadcast
        var response = await receiver.ReceiveExpectedAsync<TalkPlayerServerPacket>();
        response.Should().NotBeNull();
        response.Message.Should().Be("integration test message");
    }

    [Fact]
    public async Task Sit_AfterEnteringWorld_ShouldNotDisconnect()
    {
        await using var client = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);
        await client.InitAsync();
        await client.SendConnectionAcceptAsync();
        await client.CreateAccountAndEnterWorldAsync();

        // Send a sit request — handler is currently a stub,
        // but verify the server doesn't disconnect us.
        await client.SendSitAsync();

        // Verify the connection is still alive by performing another action
        await Task.Delay(200);
        await client.SendFaceAsync(Direction.Right);
    }

    [Fact]
    public async Task MultipleClients_ShouldCoexistOnSameServer()
    {
        // Two clients connect and enter the world simultaneously
        await using var client1 = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);
        await client1.InitAsync();
        await client1.SendConnectionAcceptAsync();
        await client1.CreateAccountAndEnterWorldAsync();

        await using var client2 = await EoTestClient.ConnectTcpAsync(_fixture.TcpPort);
        await client2.InitAsync();
        await client2.SendConnectionAcceptAsync();
        await client2.CreateAccountAndEnterWorldAsync();

        // Client 1 sends a chat message
        await client1.SendChatAsync("hello from client 1");

        // Client 2 should receive the broadcast (chat excludes the sender)
        var msg = await client2.ReceiveExpectedAsync<TalkPlayerServerPacket>();
        msg.Message.Should().Be("hello from client 1");
    }
}
