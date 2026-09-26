using System.Security.Cryptography;
using Acorn.Infrastructure.Security;
using FluentAssertions;

namespace Acorn.Tests.Infrastructure.Security;

public class HashTests
{
    [Test]
    public void HashPassword_ThenVerify_ShouldSucceed()
    {
        var hash = Hash.HashPassword("someuser", "password123", 10000, out var salt);

        Hash.VerifyPassword("someuser", "password123", salt, hash).Should().BeTrue();
    }

    [Test]
    public void VerifyPassword_WrongPassword_ShouldFail()
    {
        var hash = Hash.HashPassword("someuser", "password123", 10000, out var salt);

        Hash.VerifyPassword("someuser", "wrongpassword", salt, hash).Should().BeFalse();
    }

    [Test]
    public void VerifyPassword_LegacySaltFormat_ShouldSucceed()
    {
        // Legacy rows store a plain base64 salt with 10k-iteration PBKDF2 hashes.
        var saltBytes = RandomNumberGenerator.GetBytes(16);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2("someuser" + "password123", saltBytes,
            10000, HashAlgorithmName.SHA256, 32);

        var salt = Convert.ToBase64String(saltBytes);
        var stored = Convert.ToBase64String(hashBytes);

        Hash.VerifyPassword("someuser", "password123", salt, stored).Should().BeTrue();
        Hash.VerifyPassword("someuser", "wrongpassword", salt, stored).Should().BeFalse();
    }

    [Test]
    public void VerifyPassword_MalformedSaltOrHash_ShouldFail()
    {
        Hash.VerifyPassword("someuser", "password123", "not-base64!!!", "also-not-base64!!!")
            .Should().BeFalse();
    }

    [Test]
    public void HashPassword_ShouldEmbedIterationCount()
    {
        Hash.HashPassword("someuser", "password123", 600000, out var salt);

        salt.Should().Contain("600000");
    }
}
