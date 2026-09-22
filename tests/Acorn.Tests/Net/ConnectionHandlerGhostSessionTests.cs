using Acorn.Database.Repository;
using Acorn.Game.Mappers;
using Acorn.Infrastructure;
using Acorn.Infrastructure.Communicators;
using Acorn.Infrastructure.Telemetry;
using Acorn.Net;
using Acorn.Net.PacketHandlers;
using Acorn.Tests.TestSupport;
using Acorn.World;
using Acorn.World.Map;
using Acorn.World.Services.Party;
using Acorn.World.Services.Quest;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Acorn.Tests.Net;

/// <summary>
///     Regression tests for the connection-accept lifecycle (issue #133): a connection that
///     dies before its first packet must never leave a ghost entry in <see cref="WorldState"/>.
///     This happened because the read loop was started inside the PlayerState constructor, so
///     an instantly-reset connection completed its disposal path before TryAddPlayer ran, and
///     the late registration resurrected the dead session forever.
/// </summary>
public class ConnectionHandlerGhostSessionTests
{
    private const int FixedSessionId = 500;

    [Test]
    public async Task AcceptConnection_WhenConnectionResetsImmediately_ShouldReapPlayerWithoutGhost()
    {
        // Arrange
        var world = CreateWorldState();
        var handler = CreateHandler(world, FixedSessionId);
        var communicator = new ResetCommunicator();

        // Act — accept a connection whose socket is already dead
        handler.AcceptConnection(communicator);

        // Assert — the entry (if briefly present) must be gone and stay gone
        await WaitForCondition(() => world.Players.Count == 0, "dead connection to be removed");
        world.Players.Should().BeEmpty("a connection that died must not leave a ghost session");
        communicator.Closed.Should().BeTrue("the dead transport should be closed");
    }

    [Test]
    public async Task AcceptConnection_WhenSessionIdAlreadyTaken_ShouldAbortNewPlayerAndKeepExisting()
    {
        // Arrange — an unrelated player already owns the id the generator hands out
        var world = CreateWorldState();
        var (resident, _) = FakePlayer.Create("Resident", FixedSessionId);
        world.TryAddPlayer(FixedSessionId, resident).Should().BeTrue();

        var handler = CreateHandler(world, FixedSessionId);
        var newcomer = new ResetCommunicator();

        // Act
        handler.AcceptConnection(newcomer);

        // Assert — the newcomer is torn down, the resident instance survives
        await WaitForCondition(() => newcomer.Closed, "unregistered connection to be closed");
        world.Players.Should().HaveCount(1);
        world.GetPlayer(FixedSessionId).Should().BeSameAs(resident);
    }

    [Test]
    public async Task AcceptConnection_WhenRegistrationSucceeds_ShouldStillBeListeningAfterAdd()
    {
        // Arrange
        var world = CreateWorldState();
        var handler = CreateHandler(world, FixedSessionId);
        var communicator = new IdleCommunicator();

        // Act — a healthy connection sits registered while its (blocking) read is pending
        handler.AcceptConnection(communicator);

        // Assert
        world.GetPlayer(FixedSessionId).Should().NotBeNull();
        await WaitForCondition(() => communicator.ReceiveStarted, "the read loop to start once registered");
    }

    [Test]
    public async Task CreatePlayerState_ShouldNotStartReadLoopUntilStartListening()
    {
        // Arrange
        var factory = CreatePlayerStateFactory();
        var communicator = new IdleCommunicator();

        // Act — construction alone must not run the loop; registration happens in between
        var player = factory.CreatePlayerState(communicator, FixedSessionId, _ => Task.CompletedTask);

        // Assert
        communicator.ReceiveStarted.Should().BeFalse("the read loop must not race world registration");

        // Act again
        player.StartListening();
        await WaitForCondition(() => communicator.ReceiveStarted, "the read loop to start");
    }

    // --- Helpers ---

    private static ConnectionHandler CreateHandler(WorldState world, int sessionId)
    {
        var provider = CreateServices();
        var sessionGenerator = Substitute.For<ISessionGenerator>();
        sessionGenerator.Generate().Returns(sessionId);

        return new ConnectionHandler(
            NullLogger<ConnectionHandler>.Instance,
            world,
            Substitute.For<ICharacterMapper>(),
            sessionGenerator,
            CreatePlayerStateFactory(provider),
            Substitute.For<IPartyService>(),
            Substitute.For<IQuestService>(),
            provider.GetRequiredService<IServiceScopeFactory>());
    }

    private static PlayerStateFactory CreatePlayerStateFactory() => CreatePlayerStateFactory(CreateServices());

    private static PlayerStateFactory CreatePlayerStateFactory(ServiceProvider provider)
    {
        return new PlayerStateFactory(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PlayerState>.Instance,
            Microsoft.Extensions.Options.Options.Create(FakePlayer.CreateOptions()),
            new AcornMetrics());
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IEnumerable<IPacketHandler>>(Array.Empty<IPacketHandler>());
        return services.BuildServiceProvider();
    }

    private static WorldState CreateWorldState()
    {
        var dataFiles = Substitute.For<IDataFileRepository>();
        dataFiles.Maps.Returns(Array.Empty<MapWithId>());
        return new WorldState(dataFiles, null!, NullLogger<WorldState>.Instance);
    }

    private static async Task WaitForCondition(Func<bool> condition, string description)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(25);
        }

        condition().Should().BeTrue($"timed out waiting for {description}");
    }

    /// <summary>
    ///     Communicator whose Receive() throws the IOException an instantly-reset socket
    ///     produces, so the read loop terminates its cleanup chain synchronously.
    /// </summary>
    private sealed class ResetCommunicator : ICommunicator
    {
        public bool Closed { get; private set; }

        public bool IsConnected => !Closed;

        public Task Send(IEnumerable<byte> bytes) => Task.CompletedTask;

        public Stream Receive() => throw new IOException("Connection reset by peer (simulated)");

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            Closed = true;
            return Task.CompletedTask;
        }

        public string GetConnectionOrigin() => "test-reset";
    }

    /// <summary>
    ///     Communicator whose Receive() hands out a stream that never completes, emulating a
    ///     live connection with no traffic.
    /// </summary>
    private sealed class IdleCommunicator : ICommunicator
    {
        public bool ReceiveStarted { get; private set; }

        public bool IsConnected => true;

        public Task Send(IEnumerable<byte> bytes) => Task.CompletedTask;

        public Stream Receive()
        {
            ReceiveStarted = true;
            return new NeverEndingStream();
        }

        public Task CloseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public string GetConnectionOrigin() => "test-idle";
    }

    /// <summary>
    ///     Stream whose reads block until cancelled, emulating a live socket with no traffic.
    ///     <see cref="PlayerState.Listen"/> only ever reads asynchronously.
    /// </summary>
    private sealed class NeverEndingStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position { get; set; }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException("Use async reads only.");

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            new(BlockUntilCancelledAsync(cancellationToken));

        private static async Task<int> BlockUntilCancelledAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public override void Write(byte[] buffer, int offset, int count)
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => 0;

        public override void SetLength(long value)
        {
        }
    }
}
