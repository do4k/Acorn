using Acorn.Net;
using FluentAssertions;
using SdkVersion = Moffat.EndlessOnline.SDK.Protocol.Net.Version;

namespace Acorn.Tests.Net;

public class ClientVersionValidatorTests
{
    private static SdkVersion V(int major, int minor, int patch) =>
        new() { Major = major, Minor = minor, Patch = patch };

    [Test]
    public void IsSupported_WhenVersionWithinRange_ShouldReturnTrue()
    {
        ClientVersionValidator.IsSupported(V(0, 0, 28), "0.0.28", "0.3.29").Should().BeTrue();
    }

    [Test]
    public void IsSupported_WhenVersionIsMinimum_ShouldReturnTrue()
    {
        ClientVersionValidator.IsSupported(V(0, 0, 28), "0.0.28", "0.3.29").Should().BeTrue();
    }

    [Test]
    public void IsSupported_WhenVersionIsMaximum_ShouldReturnTrue()
    {
        ClientVersionValidator.IsSupported(V(0, 3, 29), "0.0.28", "0.3.29").Should().BeTrue();
    }

    [Test]
    public void IsSupported_WhenVersionTooOld_ShouldReturnFalse()
    {
        ClientVersionValidator.IsSupported(V(0, 0, 27), "0.0.28", "0.3.29").Should().BeFalse();
    }

    [Test]
    public void IsSupported_WhenVersionTooNew_ShouldReturnFalse()
    {
        ClientVersionValidator.IsSupported(V(1, 0, 0), "0.0.28", "0.3.29").Should().BeFalse();
    }

    [Test]
    public void IsSupported_WhenMaxVersionIsUnlimited_ShouldAcceptNewerVersions()
    {
        ClientVersionValidator.IsSupported(V(9, 9, 9), "0.0.28", "-1").Should().BeTrue();
    }

    [Test]
    public void IsSupported_WhenMinVersionMalformed_ShouldFailOpen()
    {
        ClientVersionValidator.IsSupported(V(0, 0, 1), "not-a-version", "0.3.29").Should().BeTrue();
    }
}