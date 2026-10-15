using Acorn.Infrastructure.Plugins;
using Acorn.Options;
using FluentAssertions;

namespace Acorn.Tests.Plugins;

/// <summary>
///     Manifest/config validation for plugin discovery. Assembly loading itself is
///     covered by <see cref="PluginLoadIntegrationTests" />; these tests lock down
///     the fail-fast behaviour of every rejection path.
/// </summary>
public class PluginDiscoveryTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"acorn_plugins_{Guid.NewGuid():N}");

    [After(Test)]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private PluginsOptions OptionsWith(params string[] load) => new()
    {
        Enabled = true,
        Directory = _root,
        Load = load
    };

    private string CreatePluginDirectory(string id)
    {
        var directory = Path.Combine(_root, id);
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static string ManifestJson(
        string id,
        int contractVersion = 1,
        string entryAssembly = "TestPlugin.dll") =>
        $$"""
          {
            "id": "{{id}}",
            "name": "Test Plugin",
            "version": "1.0.0",
            "entryAssembly": "{{entryAssembly}}",
            "contractVersion": {{contractVersion}}
          }
          """;

    private static void WriteManifest(string directory, string json) =>
        File.WriteAllText(Path.Combine(directory, "plugin.json"), json);

    [Test]
    public void Discover_WhenSystemDisabled_ShouldReturnEmptyCatalogWithoutTouchingDisk()
    {
        // Arrange — _root deliberately does not exist
        var options = OptionsWith("some-plugin");
        options.Enabled = false;

        // Act
        var catalog = PluginDiscovery.Discover(options);

        // Assert
        catalog.Entries.Should().BeEmpty();
    }

    [Test]
    public void Discover_WhenNothingConfiguredToLoad_ShouldReturnEmptyCatalog()
    {
        // Arrange — enabled but no plugin ids requested
        var options = OptionsWith();

        // Act
        var catalog = PluginDiscovery.Discover(options);

        // Assert
        catalog.Entries.Should().BeEmpty();
    }

    [Test]
    public void Discover_WhenPluginsDirectoryMissing_ShouldThrow()
    {
        // Arrange — rooted directory that does not exist
        var options = OptionsWith("some-plugin");

        // Act
        var act = () => PluginDiscovery.Discover(options);

        // Assert
        act.Should().Throw<PluginLoadException>().WithMessage("*does not exist*");
    }

    [Test]
    public void Discover_WhenRelativePluginsDirectoryMissing_ShouldThrowListingProbedPaths()
    {
        // Arrange — relative path probed against base dir and working directory
        var options = new PluginsOptions
        {
            Enabled = true,
            Directory = "definitely_not_a_plugins_dir",
            Load = ["some-plugin"]
        };

        // Act
        var act = () => PluginDiscovery.Discover(options);

        // Assert
        act.Should().Throw<PluginLoadException>().WithMessage("*was not found (probed:*");
    }

    [Test]
    public void Discover_WhenRequestedPluginFolderMissing_ShouldThrow()
    {
        // Arrange
        Directory.CreateDirectory(_root);

        // Act
        var act = () => PluginDiscovery.Discover(OptionsWith("missing-plugin"));

        // Assert
        act.Should().Throw<PluginLoadException>().WithMessage("*no plugin directory*");
    }

    [Test]
    public void Discover_WhenManifestMissing_ShouldThrow()
    {
        // Arrange
        CreatePluginDirectory("no-manifest");

        // Act
        var act = () => PluginDiscovery.Discover(OptionsWith("no-manifest"));

        // Assert
        act.Should().Throw<PluginLoadException>().WithMessage("*missing manifest*");
    }

    [Test]
    public void Discover_WhenManifestIsNotValidJson_ShouldThrow()
    {
        // Arrange
        var directory = CreatePluginDirectory("bad-json");
        File.WriteAllText(Path.Combine(directory, "plugin.json"), "{ not json ");

        // Act
        var act = () => PluginDiscovery.Discover(OptionsWith("bad-json"));

        // Assert
        act.Should().Throw<PluginLoadException>().WithMessage("*invalid manifest*");
    }

    [Test]
    public void Discover_WhenManifestIdDoesNotMatchDirectory_ShouldThrow()
    {
        // Arrange
        var directory = CreatePluginDirectory("folder-name");
        WriteManifest(directory, ManifestJson("different-id"));

        // Act
        var act = () => PluginDiscovery.Discover(OptionsWith("folder-name"));

        // Assert
        act.Should().Throw<PluginLoadException>().WithMessage("*does not match*");
    }

    [Test]
    public void Discover_WhenContractVersionMismatch_ShouldThrow()
    {
        // Arrange
        var directory = CreatePluginDirectory("future-plugin");
        WriteManifest(directory, ManifestJson("future-plugin", contractVersion: 999));

        // Act
        var act = () => PluginDiscovery.Discover(OptionsWith("future-plugin"));

        // Assert
        act.Should().Throw<PluginLoadException>().WithMessage("*contract v999*");
    }

    [Test]
    public void Discover_WhenEntryAssemblyMissing_ShouldThrow()
    {
        // Arrange — valid manifest, no dll on disk
        var directory = CreatePluginDirectory("no-dll");
        WriteManifest(directory, ManifestJson("no-dll"));

        // Act
        var act = () => PluginDiscovery.Discover(OptionsWith("no-dll"));

        // Assert
        act.Should().Throw<PluginLoadException>().WithMessage("*not found*");
    }

    [Test]
    public void Discover_WhenEntryAssemblyEscapesPluginDirectory_ShouldThrow()
    {
        // Arrange — traversal must be rejected before the file check runs
        var directory = CreatePluginDirectory("traversal");
        WriteManifest(directory, ManifestJson("traversal", entryAssembly: "../evil.dll"));

        // Act
        var act = () => PluginDiscovery.Discover(OptionsWith("traversal"));

        // Assert
        act.Should().Throw<PluginLoadException>().WithMessage("*outside*");
    }

    [Test]
    public void Discover_WhenEntryAssemblyIsRooted_ShouldThrow()
    {
        // Arrange
        var directory = CreatePluginDirectory("rooted");
        WriteManifest(directory, ManifestJson("rooted", entryAssembly: "/etc/passwd"));

        // Act
        var act = () => PluginDiscovery.Discover(OptionsWith("rooted"));

        // Assert
        act.Should().Throw<PluginLoadException>().WithMessage("*relative file name*");
    }

    [Test]
    public void Discover_WhenPluginIdsRepeat_ShouldLoadEachPluginOnce()
    {
        // Arrange — the sample plugin dll from the test output, requested with
        // duplicate and mixed-case ids
        var directory = CreatePluginDirectory("hello-acorn");
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "HelloAcorn.dll"),
            Path.Combine(directory, "HelloAcorn.dll"));
        WriteManifest(directory, ManifestJson("hello-acorn", entryAssembly: "HelloAcorn.dll"));
        var options = OptionsWith("hello-acorn", "hello-acorn", "HELLO-ACORN");

        // Act
        var catalog = PluginDiscovery.Discover(options);

        // Assert
        catalog.Entries.Should().HaveCount(1);
        catalog.Entries[0].Manifest.Id.Should().Be("hello-acorn");
    }
}
