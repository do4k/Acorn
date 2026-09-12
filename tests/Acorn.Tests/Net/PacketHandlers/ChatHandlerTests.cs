using Acorn.Game.Services;
using Acorn.Net;
using Acorn.Net.PacketHandlers.Player.Talk;
using Acorn.Tests.TestSupport;
using Acorn.World;
using Acorn.World.Services.Guild;
using Acorn.World.Services.Party;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moffat.EndlessOnline.SDK.Protocol;
using Moffat.EndlessOnline.SDK.Protocol.Net.Client;
using NSubstitute;
using System.Threading.Tasks;

namespace Acorn.Tests.Net.PacketHandlers;

public class ChatHandlerTests
{
    private static IChatSanitizer PassthroughSanitizer()
    {
        var sanitizer = Substitute.For<IChatSanitizer>();
        sanitizer.Sanitize(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(ci => ci.ArgAt<string>(0));
        return sanitizer;
    }

    // --- Party chat ---

    [Test]
    public async Task PartyChat_WhenMuted_DoesNotBroadcast()
    {
        var party = Substitute.For<IPartyService>();
        var handler = new TalkOpenClientPacketHandler(party, PassthroughSanitizer());
        var (player, _) = FakePlayer.Create();
        player.MutedUntil = DateTime.UtcNow.AddMinutes(1);

        await handler.HandleAsync(player, new TalkOpenClientPacket { Message = "hi" });

        await party.DidNotReceiveWithAnyArgs().SendPartyMessage(default!, default!);
    }

    [Test]
    public async Task PartyChat_WhenNotMuted_Broadcasts()
    {
        var party = Substitute.For<IPartyService>();
        var handler = new TalkOpenClientPacketHandler(party, PassthroughSanitizer());
        var (player, _) = FakePlayer.Create();

        await handler.HandleAsync(player, new TalkOpenClientPacket { Message = "hi" });

        await party.Received(1).SendPartyMessage(player, "hi");
    }

    // --- Guild chat ---

    [Test]
    public async Task GuildChat_WhenMuted_DoesNotBroadcast()
    {
        var guild = Substitute.For<IGuildService>();
        var handler = new TalkRequestClientPacketHandler(guild, PassthroughSanitizer());
        var (player, _) = FakePlayer.Create();
        player.Character!.GuildTag = "TEST";
        player.MutedUntil = DateTime.UtcNow.AddMinutes(1);

        await handler.HandleAsync(player, new TalkRequestClientPacket { Message = "hi" });

        await guild.DidNotReceiveWithAnyArgs().SendGuildMessage(default!, default!);
    }

    [Test]
    public async Task GuildChat_WhenNotMuted_Broadcasts()
    {
        var guild = Substitute.For<IGuildService>();
        var handler = new TalkRequestClientPacketHandler(guild, PassthroughSanitizer());
        var (player, _) = FakePlayer.Create();
        player.Character!.GuildTag = "TEST";

        await handler.HandleAsync(player, new TalkRequestClientPacket { Message = "hi" });

        await guild.Received(1).SendGuildMessage(player, "hi");
    }

    // --- Global chat ---

    [Test]
    public async Task GlobalChat_WhenMuted_DoesNotAddMessage()
    {
        var world = Substitute.For<IWorldQueries>();
        var handler = new TalkMsgClientPacketHandler(world, PassthroughSanitizer());
        var (player, _) = FakePlayer.Create();
        player.MutedUntil = DateTime.UtcNow.AddMinutes(1);

        await handler.HandleAsync(player, new TalkMsgClientPacket { Message = "hi" });

        world.DidNotReceiveWithAnyArgs().AddGlobalMessage(default!);
    }

    [Test]
    public async Task GlobalChat_WhenJailed_DoesNotAddMessage()
    {
        var world = Substitute.For<IWorldQueries>();
        var handler = new TalkMsgClientPacketHandler(world, PassthroughSanitizer());
        var (player, _) = FakePlayer.Create();
        player.IsJailed = true;

        await handler.HandleAsync(player, new TalkMsgClientPacket { Message = "hi" });

        world.DidNotReceiveWithAnyArgs().AddGlobalMessage(default!);
    }

    [Test]
    public async Task GlobalChat_WhenNotMutedOrJailed_AddsMessage()
    {
        var world = Substitute.For<IWorldQueries>();
        world.GetGlobalChatListeners().Returns(Array.Empty<PlayerState>());
        var handler = new TalkMsgClientPacketHandler(world, PassthroughSanitizer());
        var (player, _) = FakePlayer.Create();

        await handler.HandleAsync(player, new TalkMsgClientPacket { Message = "hi" });

        world.Received(1).AddGlobalMessage(Arg.Any<GlobalMessage>());
    }

    // --- Tell / whisper ---

    [Test]
    public async Task Tell_WhenSenderMuted_DoesNotDeliver()
    {
        var world = Substitute.For<IWorldQueries>();
        var (target, targetComms) = FakePlayer.Create("Target", 2);
        world.FindPlayerByName("Target").Returns(target);
        var handler = new TalkTellClientPacketHandler(world, PassthroughSanitizer());
        var (sender, senderComms) = FakePlayer.Create("Sender", 1);
        sender.MutedUntil = DateTime.UtcNow.AddMinutes(1);

        await handler.HandleAsync(sender, new TalkTellClientPacket { Name = "Target", Message = "hi" });

        targetComms.Sent.Should().BeEmpty();
        senderComms.Sent.Should().BeEmpty();
    }

    [Test]
    public async Task Tell_WhenTargetWhispersOff_RepliesNotFound_AndDoesNotDeliver()
    {
        var world = Substitute.For<IWorldQueries>();
        var (target, targetComms) = FakePlayer.Create("Target", 2);
        target.Whispers = false;
        world.FindPlayerByName("Target").Returns(target);
        var handler = new TalkTellClientPacketHandler(world, PassthroughSanitizer());
        var (sender, senderComms) = FakePlayer.Create("Sender", 1);

        await handler.HandleAsync(sender, new TalkTellClientPacket { Name = "Target", Message = "hi" });

        targetComms.Sent.Should().BeEmpty();
        senderComms.Sent.Should().HaveCount(1);
    }

    [Test]
    public async Task Tell_WhenTargetHidden_RepliesNotFound_AndDoesNotDeliver()
    {
        var world = Substitute.For<IWorldQueries>();
        var (target, targetComms) = FakePlayer.Create("Target", 2);
        target.Character!.Hidden = true;
        world.FindPlayerByName("Target").Returns(target);
        var handler = new TalkTellClientPacketHandler(world, PassthroughSanitizer());
        var (sender, senderComms) = FakePlayer.Create("Sender", 1);

        await handler.HandleAsync(sender, new TalkTellClientPacket { Name = "Target", Message = "hi" });

        targetComms.Sent.Should().BeEmpty();
        senderComms.Sent.Should().HaveCount(1);
    }

    [Test]
    public async Task Tell_WhenTargetNotFound_RepliesNotFound()
    {
        var world = Substitute.For<IWorldQueries>();
        world.FindPlayerByName("Ghost").Returns((PlayerState?)null);
        var handler = new TalkTellClientPacketHandler(world, PassthroughSanitizer());
        var (sender, senderComms) = FakePlayer.Create("Sender", 1);

        await handler.HandleAsync(sender, new TalkTellClientPacket { Name = "Ghost", Message = "hi" });

        senderComms.Sent.Should().HaveCount(1);
    }

    [Test]
    public async Task Tell_WhenValid_DeliversToTarget()
    {
        var world = Substitute.For<IWorldQueries>();
        var (target, targetComms) = FakePlayer.Create("Target", 2);
        world.FindPlayerByName("Target").Returns(target);
        var handler = new TalkTellClientPacketHandler(world, PassthroughSanitizer());
        var (sender, senderComms) = FakePlayer.Create("Sender", 1);

        await handler.HandleAsync(sender, new TalkTellClientPacket { Name = "Target", Message = "hi" });

        targetComms.Sent.Should().HaveCount(1);
        senderComms.Sent.Should().BeEmpty();
    }

    // --- Admin chat thresholds / mute ---

    [Test]
    public async Task AdminChat_WhenSpy_DoesNotBroadcast()
    {
        var world = Substitute.For<IWorldQueries>();
        var handler = new TalkAdminClientPacketHandler(world, PassthroughSanitizer(),
            NullLogger<TalkAdminClientPacketHandler>.Instance);
        var (player, _) = FakePlayer.Create();
        player.Character!.Admin = AdminLevel.Spy;

        await handler.HandleAsync(player, new TalkAdminClientPacket { Message = "hi" });

        world.DidNotReceive().GetAllPlayers();
    }

    [Test]
    public async Task AdminChat_WhenGuardian_Broadcasts()
    {
        var world = Substitute.For<IWorldQueries>();
        world.GetAllPlayers().Returns(Array.Empty<PlayerState>());
        var handler = new TalkAdminClientPacketHandler(world, PassthroughSanitizer(),
            NullLogger<TalkAdminClientPacketHandler>.Instance);
        var (player, _) = FakePlayer.Create();
        player.Character!.Admin = AdminLevel.Guardian;

        await handler.HandleAsync(player, new TalkAdminClientPacket { Message = "hi" });

        world.Received(1).GetAllPlayers();
    }

    [Test]
    public async Task AdminChat_WhenGuardianMuted_DoesNotBroadcast()
    {
        var world = Substitute.For<IWorldQueries>();
        var handler = new TalkAdminClientPacketHandler(world, PassthroughSanitizer(),
            NullLogger<TalkAdminClientPacketHandler>.Instance);
        var (player, _) = FakePlayer.Create();
        player.Character!.Admin = AdminLevel.Guardian;
        player.MutedUntil = DateTime.UtcNow.AddMinutes(1);

        await handler.HandleAsync(player, new TalkAdminClientPacket { Message = "hi" });

        world.DidNotReceive().GetAllPlayers();
    }

    // --- Announce thresholds / mute ---

    [Test]
    public async Task Announce_WhenSpy_DoesNotBroadcast()
    {
        var world = Substitute.For<IWorldQueries>();
        var handler = new TalkAnnounceClientPacketHandler(world, PassthroughSanitizer(),
            NullLogger<TalkAnnounceClientPacketHandler>.Instance);
        var (player, _) = FakePlayer.Create();
        player.Character!.Admin = AdminLevel.Spy;

        await handler.HandleAsync(player, new TalkAnnounceClientPacket { Message = "hi" });

        world.DidNotReceive().GetAllPlayers();
    }

    [Test]
    public async Task Announce_WhenGuardian_Broadcasts()
    {
        var world = Substitute.For<IWorldQueries>();
        world.GetAllPlayers().Returns(Array.Empty<PlayerState>());
        var handler = new TalkAnnounceClientPacketHandler(world, PassthroughSanitizer(),
            NullLogger<TalkAnnounceClientPacketHandler>.Instance);
        var (player, _) = FakePlayer.Create();
        player.Character!.Admin = AdminLevel.Guardian;

        await handler.HandleAsync(player, new TalkAnnounceClientPacket { Message = "hi" });

        world.Received(1).GetAllPlayers();
    }

    [Test]
    public async Task Announce_WhenGuardianMuted_DoesNotBroadcast()
    {
        var world = Substitute.For<IWorldQueries>();
        var handler = new TalkAnnounceClientPacketHandler(world, PassthroughSanitizer(),
            NullLogger<TalkAnnounceClientPacketHandler>.Instance);
        var (player, _) = FakePlayer.Create();
        player.Character!.Admin = AdminLevel.Guardian;
        player.MutedUntil = DateTime.UtcNow.AddMinutes(1);

        await handler.HandleAsync(player, new TalkAnnounceClientPacket { Message = "hi" });

        world.DidNotReceive().GetAllPlayers();
    }

    // --- Muted players cannot run commands ---

    [Test]
    public async Task Report_WhenMutedAdmin_SkipsDollarCommands()
    {
        var talkHandler = Substitute.For<ITalkHandler>();
        talkHandler.CanHandle("global").Returns(true);
        var handler = new TalkReportClientPacketHandler(
            [talkHandler],
            Array.Empty<IPlayerCommandHandler>(),
            null!,
            null!,
            PassthroughSanitizer());
        var (player, _) = FakePlayer.Create();
        player.Character!.Admin = AdminLevel.Guardian;
        player.MutedUntil = DateTime.UtcNow.AddMinutes(1);

        await handler.HandleAsync(player, new TalkReportClientPacket { Message = "$global hello" });

        await talkHandler.DidNotReceiveWithAnyArgs()
            .HandleAsync(Arg.Any<PlayerState>(), Arg.Any<string>(), Arg.Any<string[]>());
    }

    [Test]
    public async Task Report_WhenMuted_SkipsPlayerCommands()
    {
        var playerCommand = Substitute.For<IPlayerCommandHandler>();
        playerCommand.CanHandle("help").Returns(true);
        var handler = new TalkReportClientPacketHandler(
            Array.Empty<ITalkHandler>(),
            [playerCommand],
            null!,
            null!,
            PassthroughSanitizer());
        var (player, _) = FakePlayer.Create();
        player.MutedUntil = DateTime.UtcNow.AddMinutes(1);

        await handler.HandleAsync(player, new TalkReportClientPacket { Message = "#help" });

        await playerCommand.DidNotReceiveWithAnyArgs()
            .HandleAsync(Arg.Any<PlayerState>(), Arg.Any<string>(), Arg.Any<string[]>());
    }
}