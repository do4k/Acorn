using Acorn.Game.Validation;
using FluentAssertions;
using Xunit;

namespace Acorn.Tests.Game.Validation;

public class PlayerValidationTests
{
    [Theory]
    [InlineData("acorn")]
    [InlineData("abc123")]
    [InlineData("bob smith")]
    [InlineData("1234")]
    public void IsValidAccountName_WhenAllowedCharacters_ShouldReturnTrue(string name)
    {
        PlayerValidation.IsValidAccountName(name).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Acorn")]
    [InlineData("abc_123")]
    [InlineData("abc-123")]
    [InlineData("bob!")]
    public void IsValidAccountName_WhenDisallowedCharacters_ShouldReturnFalse(string? name)
    {
        PlayerValidation.IsValidAccountName(name).Should().BeFalse();
    }

    [Theory]
    [InlineData("bobby")]
    [InlineData("bob smith")]
    [InlineData("bob123")]
    [InlineData("a234")]
    public void IsValidCharacterName_WhenValid_ShouldReturnTrue(string name)
    {
        PlayerValidation.IsValidCharacterName(name).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("bob")] // too short
    [InlineData("thisnameistoolong")] // too long
    [InlineData("Bob")] // uppercase
    [InlineData("bob!")] // symbol
    [InlineData("    ")] // whitespace only
    [InlineData("server")] // reserved
    public void IsValidCharacterName_WhenInvalid_ShouldReturnFalse(string? name)
    {
        PlayerValidation.IsValidCharacterName(name).Should().BeFalse();
    }

    [Fact]
    public void NormalizeName_ShouldLowercase()
    {
        PlayerValidation.NormalizeName("BoB Smith").Should().Be("bob smith");
    }
}
