using Acorn.Game.Services;
using Acorn.Net.PacketHandlers.Player;
using Acorn.Net.PacketHandlers.Player.Talk;
using Acorn.Tests.TestSupport;
using Acorn.World.Services.Map;
using FluentAssertions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using NSubstitute;
using System.Threading.Tasks;

namespace Acorn.Tests.Net.PacketHandlers;

/// <summary>
///     Verifies that local chat, emotes and bard music only reach players within
///     client render range.
/// </summary>
public class ChatRangeTests
{
    private static IChatSanitizer PassthroughSanitizer()
    {
        var sanitizer = Substitute.For<IChatSanitizer>();
        sanitizer.Sanitize(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(ci => ci.ArgAt<string>(0));
        return sanitizer;
    }

    private static TalkReportClientPacketHandler CreateChatHandler()
    {
        return new TalkReportClientPacketHandler(
            Array.Empty<ITalkHandler>(),
            Array.Empty<IPlayerCommandHandler>(),
            FakePlayer.CreateWiseManHandler(),
            new MapTileService(),
            PassthroughSanitizer());
    }

    [Test]
    public async Task LocalChat_WhenOutOfRange_DoesNotReachPlayer()
    {
        var map = FakeMap.Create();
        var (sender, _) = FakePlayer.Create("Sender", 1);
        var (observer, observerComms) = FakePlayer.Create("Observer", 2);
        sender.Character!.X = 0;
        sender.Character.Y = 0;
        observer.Character!.X = 19;
        observer.Character.Y = 19;
        sender.CurrentMap = map;
        observer.CurrentMap = map;
        map.Players.TryAdd(1, sender);
        map.Players.TryAdd(2, observer);

        await CreateChatHandler().HandleAsync(sender, new TalkReportClientPacket { Message = "hi" });

        observerComms.Sent.Should().BeEmpty();
    }

    [Test]
    public async Task LocalChat_WhenInRange_ReachesPlayer()
    {
        var map = FakeMap.Create();
        var (sender, _) = FakePlayer.Create("Sender", 1);
        var (observer, observerComms) = FakePlayer.Create("Observer", 2);
        sender.Character!.X = 0;
        sender.Character.Y = 0;
        observer.Character!.X = 5;
        observer.Character.Y = 5;
        sender.CurrentMap = map;
        observer.CurrentMap = map;
        map.Players.TryAdd(1, sender);
        map.Players.TryAdd(2, observer);

        await CreateChatHandler().HandleAsync(sender, new TalkReportClientPacket { Message = "hi" });

        observerComms.Sent.Should().HaveCount(1);
    }

    [Test]
    public async Task Emote_WhenOutOfRange_DoesNotReachPlayer()
    {
        var map = FakeMap.Create();
        var (sender, _) = FakePlayer.Create("Sender", 1);
        var (observer, observerComms) = FakePlayer.Create("Observer", 2);
        sender.Character!.X = 0;
        sender.Character.Y = 0;
        observer.Character!.X = 19;
        observer.Character.Y = 19;
        sender.CurrentMap = map;
        observer.CurrentMap = map;
        map.Players.TryAdd(1, sender);
        map.Players.TryAdd(2, observer);

        var handler = new EmoteReportClientPacketHandler(new MapTileService());
        await handler.HandleAsync(sender, new EmoteReportClientPacket { Emote = Emote.Happy });

        observerComms.Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Emote_WhenInRange_ReachesPlayer()
    {
        var map = FakeMap.Create();
        var (sender, _) = FakePlayer.Create("Sender", 1);
        var (observer, observerComms) = FakePlayer.Create("Observer", 2);
        sender.Character!.X = 0;
        sender.Character.Y = 0;
        observer.Character!.X = 5;
        observer.Character.Y = 5;
        sender.CurrentMap = map;
        observer.CurrentMap = map;
        map.Players.TryAdd(1, sender);
        map.Players.TryAdd(2, observer);

        var handler = new EmoteReportClientPacketHandler(new MapTileService());
        await handler.HandleAsync(sender, new EmoteReportClientPacket { Emote = Emote.Happy });

        observerComms.Sent.Should().HaveCount(1);
    }
}