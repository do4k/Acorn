using Acorn.Database.Repository;
using Acorn.Game.Services;
using Acorn.Net.PacketHandlers.Player;
using Acorn.Net.PacketHandlers.Player.Talk;
using Acorn.Tests.TestSupport;
using Acorn.World;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using Moffat.EndlessOnline.SDK.Protocol.Map;
using NSubstitute;
using System.Threading.Tasks;

namespace Acorn.Tests.Net.PacketHandlers;

/// <summary>
///     Regression coverage for global chat replay: reopening the global tab must not
///     re-deliver messages the player has already seen, and the welcome is sent once
///     per connection.
/// </summary>
public class GlobalChatReplayTests
{
    private static IChatSanitizer PassthroughSanitizer()
    {
        var sanitizer = Substitute.For<IChatSanitizer>();
        sanitizer.Sanitize(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(ci => ci.ArgAt<string>(0));
        return sanitizer;
    }

    private static (WorldState World, WorldStateQueries Queries) CreateRealWorld()
    {
        var repo = Substitute.For<IDataFileRepository>();
        repo.Maps.Returns(Array.Empty<MapWithId>());
        var world = new WorldState(repo, null!, NullLogger<WorldState>.Instance);
        var queries = new WorldStateQueries(world, repo, NullLogger<WorldStateQueries>.Instance);
        return (world, queries);
    }

    [Test]
    public async Task Reopening_DoesNotReplayMessagesAlreadyReceivedLive()
    {
        var (world, queries) = CreateRealWorld();
        var (alice, _) = FakePlayer.Create("Alice", 1);
        var (bob, bobComm) = FakePlayer.Create("Bob", 2);
        world.TryAddPlayer(1, alice);
        world.TryAddPlayer(2, bob);
        bob.IsListeningToGlobal = true;

        await new TalkMsgClientPacketHandler(queries, PassthroughSanitizer())
            .HandleAsync(alice, new TalkMsgClientPacket { Message = "hello" });
        bobComm.Sent.Should().HaveCount(1, "the live message is delivered to listeners");

        var openHandler = new GlobalOpenClientPacketHandler(queries);
        await openHandler.HandleAsync(bob, new GlobalOpenClientPacket());
        bobComm.Sent.Should().HaveCount(2, "only the welcome is added on first open");

        await openHandler.HandleAsync(bob, new GlobalOpenClientPacket());
        bobComm.Sent.Should().HaveCount(2, "reopening must not duplicate the welcome or history");
    }

    [Test]
    public async Task Opening_CatchesUpOnMessagesMissedWhileNotListening()
    {
        var (world, queries) = CreateRealWorld();
        var (alice, _) = FakePlayer.Create("Alice", 1);
        var (bob, bobComm) = FakePlayer.Create("Bob", 2);
        world.TryAddPlayer(1, alice);
        world.TryAddPlayer(2, bob);

        var sendHandler = new TalkMsgClientPacketHandler(queries, PassthroughSanitizer());
        await sendHandler.HandleAsync(alice, new TalkMsgClientPacket { Message = "one" });
        await sendHandler.HandleAsync(alice, new TalkMsgClientPacket { Message = "two" });
        bobComm.Sent.Should().BeEmpty("Bob was not listening yet");

        await new GlobalOpenClientPacketHandler(queries)
            .HandleAsync(bob, new GlobalOpenClientPacket());

        bobComm.Sent.Should().HaveCount(3, "the welcome plus both missed messages are replayed");
    }
}
