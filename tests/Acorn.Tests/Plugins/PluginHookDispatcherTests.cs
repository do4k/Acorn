using Acorn.Infrastructure.Plugins;
using Acorn.Options;
using Acorn.Plugins;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Acorn.Tests.Plugins;

/// <summary>
///     Hook dispatch semantics: priority ordering, per-plugin error isolation and
///     auto-disable after consecutive failures.
/// </summary>
public class PluginHookDispatcherTests
{
    private readonly List<string> _recorder = [];

    private static WorldTickContext WorldContext() => new()
    {
        TotalTicks = 1,
        OnlinePlayerCount = 0
    };

    private static PluginHookDispatcher CreateDispatcher(int failureThreshold = 50) =>
        new(
            Microsoft.Extensions.Options.Options.Create(
                new PluginsOptions { FailureThreshold = failureThreshold }),
            NullLogger<PluginHookDispatcher>.Instance);

    private static PluginEntry CreateEntry(object instance, string id)
    {
        var pluginInstance = (IAcornPlugin)instance;
        var entry = new PluginEntry(
            new PluginManifest
            {
                Id = id,
                Name = id,
                Version = "1.0.0",
                EntryAssembly = $"{id}.dll",
                ContractVersion = 1
            },
            typeof(RecordingHookPlugin).Assembly,
            pluginInstance.GetType(),
            loadContext: null)
        {
            Instance = pluginInstance
        };
        return entry;
    }

    [Test]
    public void RegisterHooks_WhenPluginImplementsHooks_ShouldEnableDispatchGuards()
    {
        // Arrange
        var dispatcher = CreateDispatcher();
        dispatcher.HasWorldTickHooks.Should().BeFalse();
        dispatcher.HasMapTickHooks.Should().BeFalse();
        var entry = CreateEntry(new RecordingHookPlugin(_recorder, "a"), "a");

        // Act
        dispatcher.RegisterHooks(entry);

        // Assert
        dispatcher.HasWorldTickHooks.Should().BeTrue();
        dispatcher.HasMapTickHooks.Should().BeTrue();
    }

    [Test]
    public async Task RaiseWorldTickAsync_WhenPrioritiesDiffer_ShouldInvokeInPriorityOrder()
    {
        // Arrange — registered in an order that differs from priority order
        var late = new RecordingHookPlugin(_recorder, "late", priority: 10);
        var middle = new RecordingHookPlugin(_recorder, "middle", priority: 0);
        var early = new RecordingHookPlugin(_recorder, "early", priority: -5);
        var dispatcher = CreateDispatcher();
        dispatcher.RegisterHooks(CreateEntry(late, "late"));
        dispatcher.RegisterHooks(CreateEntry(middle, "middle"));
        dispatcher.RegisterHooks(CreateEntry(early, "early"));

        // Act
        await dispatcher.RaiseWorldTickAsync(WorldContext());

        // Assert
        _recorder.Should().Equal("early:world", "middle:world", "late:world");
    }

    [Test]
    public async Task RaiseWorldTickAsync_WhenOneHookThrows_ShouldStillInvokeOthersAndCountFailure()
    {
        // Arrange
        var bad = new RecordingHookPlugin(_recorder, "bad", priority: -10) { Throws = true };
        var good = new RecordingHookPlugin(_recorder, "good", priority: 0);
        var badEntry = CreateEntry(bad, "bad-plugin");
        var goodEntry = CreateEntry(good, "good-plugin");
        var dispatcher = CreateDispatcher();
        dispatcher.RegisterHooks(badEntry);
        dispatcher.RegisterHooks(goodEntry);

        // Act
        await dispatcher.RaiseWorldTickAsync(WorldContext());

        // Assert — the good plugin ran despite the failure, nothing was disabled
        _recorder.Should().Contain("good:world");
        badEntry.ConsecutiveHookFailures.Should().Be(1);
        goodEntry.ConsecutiveHookFailures.Should().Be(0);
        badEntry.Disabled.Should().BeFalse();
        goodEntry.Disabled.Should().BeFalse();
    }

    [Test]
    public async Task RaiseWorldTickAsync_WhenConsecutiveFailuresReachThreshold_ShouldAutoDisableAndSkip()
    {
        // Arrange — threshold of 2
        var bad = new RecordingHookPlugin(_recorder, "bad") { Throws = true };
        var entry = CreateEntry(bad, "bad-plugin");
        var dispatcher = CreateDispatcher(failureThreshold: 2);
        dispatcher.RegisterHooks(entry);

        // Act
        await dispatcher.RaiseWorldTickAsync(WorldContext());

        // Assert — one failure is not enough
        entry.Disabled.Should().BeFalse();

        // Act
        await dispatcher.RaiseWorldTickAsync(WorldContext());

        // Assert — second consecutive failure trips the threshold
        entry.Disabled.Should().BeTrue();

        // Act — disabled plugins are skipped entirely
        var callsBeforeSkip = _recorder.Count;
        await dispatcher.RaiseWorldTickAsync(WorldContext());

        // Assert
        _recorder.Should().HaveCount(callsBeforeSkip);
    }

    [Test]
    public async Task RaiseWorldTickAsync_WhenHookRecovers_ShouldResetFailureCount()
    {
        // Arrange
        var flaky = new RecordingHookPlugin(_recorder, "flaky") { Throws = true };
        var entry = CreateEntry(flaky, "flaky-plugin");
        var dispatcher = CreateDispatcher(failureThreshold: 5);
        dispatcher.RegisterHooks(entry);

        // Act — fail once, then recover
        await dispatcher.RaiseWorldTickAsync(WorldContext());
        flaky.Throws = false;
        await dispatcher.RaiseWorldTickAsync(WorldContext());

        // Assert
        entry.ConsecutiveHookFailures.Should().Be(0);
        entry.Disabled.Should().BeFalse();
    }
}
