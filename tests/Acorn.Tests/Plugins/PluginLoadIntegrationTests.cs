using System.Runtime.Loader;
using Acorn.Infrastructure.Plugins;
using Acorn.Net.Services;
using Acorn.Options;
using Acorn.Plugins;
using Acorn.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Acorn.Tests.Plugins;

/// <summary>
///     End-to-end plugin loading: discovers the built sample plugin from disk,
///     loads it into an isolated <see cref="AssemblyLoadContext" />, runs the
///     lifecycle and dispatches hooks and a command — proving contract type
///     identity survives the load-context boundary.
/// </summary>
public class PluginLoadIntegrationTests
{
    private string? _tempRoot;

    [After(Test)]
    public void Cleanup()
    {
        if (_tempRoot is not null && Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    /// <summary>
    ///     Stages the sample plugin (built into the test output by project
    ///     reference) into a plugin-style directory: HelloAcorn.dll + plugin.json.
    /// </summary>
    private PluginCatalog DiscoverSamplePlugin()
    {
        var sampleDll = Path.Combine(AppContext.BaseDirectory, "HelloAcorn.dll");
        File.Exists(sampleDll).Should().BeTrue(
            "samples/HelloAcorn should be referenced by the test project so its dll lands in the test output");

        _tempRoot = Path.Combine(Path.GetTempPath(), $"acorn_plugins_{Guid.NewGuid():N}");
        var pluginDirectory = Path.Combine(_tempRoot, "hello-acorn");
        Directory.CreateDirectory(pluginDirectory);

        File.Copy(sampleDll, Path.Combine(pluginDirectory, "HelloAcorn.dll"));
        File.WriteAllText(
            Path.Combine(pluginDirectory, "plugin.json"),
            """
            {
              "id": "hello-acorn",
              "name": "Hello Acorn",
              "version": "1.0.0",
              "entryAssembly": "HelloAcorn.dll",
              "contractVersion": 1
            }
            """);

        return PluginDiscovery.Discover(new PluginsOptions
        {
            Enabled = true,
            Directory = _tempRoot,
            Load = ["hello-acorn"]
        });
    }

    [Test]
    public void Discover_WithBuiltSamplePlugin_ShouldLoadIntoIsolatedContextWithUnifiedContracts()
    {
        // Act
        var catalog = DiscoverSamplePlugin();

        // Assert — discovered exactly one entry
        var entry = catalog.Entries.Should().ContainSingle().Subject;
        entry.Manifest.Name.Should().Be("Hello Acorn");
        entry.PluginType.FullName.Should().Be("HelloAcorn.HelloAcornPlugin");

        // Assert — the plugin assembly lives in its own load context...
        var pluginContext = AssemblyLoadContext.GetLoadContext(entry.Assembly);
        pluginContext.Should().NotBeNull();
        pluginContext.Should().NotBeSameAs(
            AssemblyLoadContext.GetLoadContext(typeof(IAcornPlugin).Assembly),
            "the plugin must be isolated from the host's default context");
        entry.Assembly.Location.Should().Be(Path.GetFullPath(
            Path.Combine(_tempRoot!, "hello-acorn", "HelloAcorn.dll")),
            "the assembly must come from the plugin directory, not the host output");

        // Assert — ...but contract types unify to the host's copy, otherwise the
        // cast below (and the host's) would throw InvalidCastException.
        typeof(IAcornPlugin).IsAssignableFrom(entry.PluginType).Should().BeTrue();
    }

    [Test]
    public async Task LoadedSamplePlugin_ShouldRunLifecycleDispatchHooksAndCommand()
    {
        // Arrange
        var catalog = DiscoverSamplePlugin();
        var entry = catalog.Entries[0];
        var services = new ServiceCollection().BuildServiceProvider();
        var world = new FakeWorldApi();

        // Act — instantiate + OnLoadedAsync, exactly as PluginHostedService does
        var instance = (IAcornPlugin)ActivatorUtilities.CreateInstance(services, entry.PluginType);
        var context = new PluginContext(
            entry.Manifest,
            new ConfigurationBuilder().Build(),
            NullLogger.Instance,
            world);
        await instance.OnLoadedAsync(context, CancellationToken.None);
        entry.Instance = instance;

        var dispatcher = new PluginHookDispatcher(
            Microsoft.Extensions.Options.Options.Create(new PluginsOptions()),
            NullLogger<PluginHookDispatcher>.Instance);
        dispatcher.RegisterHooks(entry);

        // Assert — hooks collected from the plugin's entry instance
        dispatcher.HasWorldTickHooks.Should().BeTrue();
        dispatcher.HasMapTickHooks.Should().BeTrue();

        // Act — raise both tick hooks; failures would auto-disable the entry
        await dispatcher.RaiseWorldTickAsync(new WorldTickContext { TotalTicks = 1, OnlinePlayerCount = 0 });
        await dispatcher.RaiseMapTickAsync(new MapTickContext
        {
            MapId = 1,
            TotalTicks = 1,
            PlayerCount = 0,
            NpcCount = 0
        });

        // Assert
        entry.ConsecutiveHookFailures.Should().Be(0);
        entry.Disabled.Should().BeFalse();

        // Assert — command surface: #hello registered through the entry instance
        var command = instance.Should().BeAssignableTo<IPluginCommand>().Subject;
        command.Commands.Should().Contain("hello");

        // Act — run the command against a fake player and notifications
        var (player, _) = FakePlayer.Create("Pluggy");
        var notifications = Substitute.For<INotificationService>();
        await command.HandleAsync(new CommandContext(new PlayerView(player), "hello", [], notifications));

        // Assert — the plugin replied through the host's notification service
        await notifications.Received().SystemMessage(
            player,
            Arg.Is<string>(m => m.Contains("Pluggy")));
    }
}
