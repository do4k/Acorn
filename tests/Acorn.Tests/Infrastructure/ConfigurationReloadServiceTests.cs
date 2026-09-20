using Acorn.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Acorn.Tests.Infrastructure;

public class ConfigurationReloadServiceTests
{
    [Test]
    public void Reload_WhenConfigurationRoot_ShouldReturnTrue()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Key"] = "Value" })
            .Build();
        var sut = new ConfigurationReloadService(configuration, NullLogger<ConfigurationReloadService>.Instance);

        sut.Reload().Should().BeTrue();
    }

    [Test]
    public void Reload_WhenConfigurationCannotReload_ShouldReturnFalse()
    {
        var configuration = Substitute.For<IConfiguration>();
        var sut = new ConfigurationReloadService(configuration, NullLogger<ConfigurationReloadService>.Instance);

        sut.Reload().Should().BeFalse();
    }
}
