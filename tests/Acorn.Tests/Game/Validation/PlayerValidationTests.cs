using Acorn.Game.Validation;
using FluentAssertions;

namespace Acorn.Tests.Game.Validation;

public class PlayerValidationTests
{
    [Test]
    [Arguments("acorn")]
    [Arguments("abc123")]
    [Arguments("bob smith")]
    [Arguments("1234")]
    public void IsValidAccountName_WhenAllowedCharacters_ShouldReturnTrue(string name)
    {
        PlayerValidation.IsValidAccountName(name).Should().BeTrue();
    }

    [Test]
    [Arguments("")]
    [Arguments(null)]
    [Arguments("Acorn")]
    [Arguments("abc_123")]
    [Arguments("abc-123")]
    [Arguments("bob!")]
    public void IsValidAccountName_WhenDisallowedCharacters_ShouldReturnFalse(string? name)
    {
        PlayerValidation.IsValidAccountName(name).Should().BeFalse();
    }

    [Test]
    [Arguments("bobby")]
    [Arguments("bob smith")]
    [Arguments("bob123")]
    [Arguments("a234")]
    public void IsValidCharacterName_WhenValid_ShouldReturnTrue(string name)
    {
        PlayerValidation.IsValidCharacterName(name).Should().BeTrue();
    }

    [Test]
    [Arguments("")]
    [Arguments(null)]
    [Arguments("bob")] // too short
    [Arguments("thisnameistoolong")] // too long
    [Arguments("Bob")] // uppercase
    [Arguments("bob!")] // symbol
    [Arguments("    ")] // whitespace only
    [Arguments("server")] // reserved
    public void IsValidCharacterName_WhenInvalid_ShouldReturnFalse(string? name)
    {
        PlayerValidation.IsValidCharacterName(name).Should().BeFalse();
    }

    [Test]
    public void NormalizeName_ShouldLowercase()
    {
        PlayerValidation.NormalizeName("BoB Smith").Should().Be("bob smith");
    }
}