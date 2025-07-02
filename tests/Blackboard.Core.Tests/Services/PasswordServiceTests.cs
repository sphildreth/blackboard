using Blackboard.Core.Services;
using Blackboard.Core.Tests.Helpers;

namespace Blackboard.Core.Tests.Services;

public class PasswordServiceTests
{
    private readonly IPasswordService _passwordService;

    public PasswordServiceTests()
    {
        _passwordService = new PasswordService();
    }

    [Fact]
    public void GenerateSalt_ShouldReturnNonEmptyString()
    {
        // Act
        var salt = _passwordService.GenerateSalt();

        // Assert
        salt.Should().NotBeNullOrEmpty();
        salt.Length.Should().BeGreaterThan(10);
    }

    [Fact]
    public void GenerateSalt_ShouldReturnDifferentValuesOnMultipleCalls()
    {
        // Act
        var salt1 = _passwordService.GenerateSalt();
        var salt2 = _passwordService.GenerateSalt();

        // Assert
        salt1.Should().NotBe(salt2);
    }

    [Fact]
    public void HashPassword_WithValidPasswordAndSalt_ShouldReturnHash()
    {
        // Arrange
        var password = "TestPassword123!";
        var salt = _passwordService.GenerateSalt();

        // Act
        var hash = _passwordService.HashPassword(password, salt);

        // Assert
        hash.Should().NotBeNullOrEmpty();
        hash.Should().NotBe(password);
        hash.Length.Should().BeGreaterThan(20);
    }

    [Fact]
    public void HashPassword_WithSamePasswordAndSalt_ShouldReturnSameHash()
    {
        // Arrange
        var password = "TestPassword123!";
        var salt = _passwordService.GenerateSalt();

        // Act
        var hash1 = _passwordService.HashPassword(password, salt);
        var hash2 = _passwordService.HashPassword(password, salt);

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ShouldReturnTrue()
    {
        // Arrange
        var password = "TestPassword123!";
        var salt = _passwordService.GenerateSalt();
        var hash = _passwordService.HashPassword(password, salt);

        // Act
        var result = _passwordService.VerifyPassword(password, hash, salt);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WithIncorrectPassword_ShouldReturnFalse()
    {
        // Arrange
        var password = "TestPassword123!";
        var wrongPassword = "WrongPassword123!";
        var salt = _passwordService.GenerateSalt();
        var hash = _passwordService.HashPassword(password, salt);

        // Act
        var result = _passwordService.VerifyPassword(wrongPassword, hash, salt);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("Password123!", true)]  // Valid: has upper, lower, digit, special
    [InlineData("password123!", false)] // Invalid: no uppercase
    [InlineData("PASSWORD123!", false)] // Invalid: no lowercase
    [InlineData("Password!", false)]    // Invalid: no digit
    [InlineData("Password123", false)]  // Invalid: no special character
    [InlineData("Pass1!", false)]       // Invalid: too short
    [InlineData("", false)]             // Invalid: empty
    public void ValidatePasswordComplexity_ShouldReturnExpectedResult(string password, bool expected)
    {
        // Arrange
        var settings = TestDataHelper.CreateSecuritySettings();

        // Act
        var result = _passwordService.ValidatePasswordComplexity(password, settings);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ValidatePasswordComplexity_WithComplexityDisabled_ShouldOnlyCheckLength()
    {
        // Arrange
        var settings = TestDataHelper.CreateSecuritySettings();
        settings.RequirePasswordComplexity = false;
        var simplePassword = "simplepassword";

        // Act
        var result = _passwordService.ValidatePasswordComplexity(simplePassword, settings);

        // Assert
        result.Should().BeTrue(); // Should pass because complexity is disabled and length is sufficient
    }

    [Theory]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(16)]
    public void GenerateSecurePassword_ShouldReturnPasswordOfRequestedLength(int length)
    {
        // Act
        var password = _passwordService.GenerateSecurePassword(length);

        // Assert
        password.Length.Should().Be(length);
    }

    [Fact]
    public void GenerateSecurePassword_ShouldMeetComplexityRequirements()
    {
        // Arrange
        var settings = TestDataHelper.CreateSecuritySettings();

        // Act
        var password = _passwordService.GenerateSecurePassword(12);

        // Assert
        _passwordService.ValidatePasswordComplexity(password, settings).Should().BeTrue();
    }

    [Fact]
    public void GenerateSecurePassword_ShouldReturnDifferentPasswordsOnMultipleCalls()
    {
        // Act
        var password1 = _passwordService.GenerateSecurePassword(12);
        var password2 = _passwordService.GenerateSecurePassword(12);

        // Assert
        password1.Should().NotBe(password2);
    }

    [Fact]
    public void VerifyPassword_WithInvalidSalt_ShouldReturnFalse()
    {
        // Arrange
        var password = "TestPassword123!";
        var validSalt = _passwordService.GenerateSalt();
        var hash = _passwordService.HashPassword(password, validSalt);
        var invalidSalt = "invalid-salt";

        // Act
        var result = _passwordService.VerifyPassword(password, hash, invalidSalt);

        // Assert
        result.Should().BeFalse();
    }
}
