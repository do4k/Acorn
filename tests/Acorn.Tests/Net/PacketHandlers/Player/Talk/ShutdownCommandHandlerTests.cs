using Acorn.Net.PacketHandlers.Player.Talk;
using Acorn.Net.Services;
using Acorn.Tests.TestSupport;
using Acorn.World.Services.Admin;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Acorn.Tests.Net.PacketHandlers.Player.Talk;

/// <summary>
///     $shutdown must announce to the world and request a graceful host stop.
/// </summary>
public class ShutdownCommandHandlerTests
{
    [Test]
    public async Task Shutdown_WithReason_AnnouncesAndStopsHost()
    {
        // Arrange
        var admin = Substitute.For<IAdminService>();
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        var notifications = Substitute.For<INotificationService>();
        var sut = new ShutdownCommandHandler(admin, lifetime, notifications);
        var (player, _) = FakePlayer.Create("Boss", 1);

        // Act
        await sut.HandleAsync(player, "shutdown", "down", "for", "maintenance");

        // Assert
        await admin.Received(1).GlobalMessageAsync(player, "down for maintenance");
        lifetime.Received(1).StopApplication();
    }

    [Test]
    public async Task Shutdown_WithoutReason_AnnouncesDefaultAndStopsHost()
    {
        // Arrange
        var admin = Substitute.For<IAdminService>();
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        var notifications = Substitute.For<INotificationService>();
        var sut = new ShutdownCommandHandler(admin, lifetime, notifications);
        var (player, _) = FakePlayer.Create("Boss", 1);

        // Act
        await sut.HandleAsync(player, "shutdown");

        // Assert
        await admin.Received(1).GlobalMessageAsync(player, "Server is shutting down.");
        lifetime.Received(1).StopApplication();
    }
}
