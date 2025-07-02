using Blackboard.Core.Models;

namespace Blackboard.Core.Tests.Models;

public class UserTests
{
    [Fact]
    public void IsLocked_WhenLockedUntilIsNull_ShouldReturnFalse()
    {
        // Arrange
        var user = new User
        {
            LockedUntil = null
        };

        // Act & Assert
        user.IsLocked.Should().BeFalse();
    }

    [Fact]
    public void IsLocked_WhenLockedUntilIsPast_ShouldReturnFalse()
    {
        // Arrange
        var user = new User
        {
            LockedUntil = DateTime.UtcNow.AddMinutes(-30)
        };

        // Act & Assert
        user.IsLocked.Should().BeFalse();
    }

    [Fact]
    public void IsLocked_WhenLockedUntilIsFuture_ShouldReturnTrue()
    {
        // Arrange
        var user = new User
        {
            LockedUntil = DateTime.UtcNow.AddMinutes(30)
        };

        // Act & Assert
        user.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void IsPasswordExpired_WhenPasswordExpiresAtIsNull_ShouldReturnFalse()
    {
        // Arrange
        var user = new User
        {
            PasswordExpiresAt = null
        };

        // Act & Assert
        user.IsPasswordExpired.Should().BeFalse();
    }

    [Fact]
    public void IsPasswordExpired_WhenPasswordExpiresAtIsFuture_ShouldReturnFalse()
    {
        // Arrange
        var user = new User
        {
            PasswordExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        // Act & Assert
        user.IsPasswordExpired.Should().BeFalse();
    }

    [Fact]
    public void IsPasswordExpired_WhenPasswordExpiresAtIsPast_ShouldReturnTrue()
    {
        // Arrange
        var user = new User
        {
            PasswordExpiresAt = DateTime.UtcNow.AddDays(-1)
        };

        // Act & Assert
        user.IsPasswordExpired.Should().BeTrue();
    }

    [Theory]
    [InlineData("", "", "")]
    [InlineData("John", "", "John")]
    [InlineData("", "Doe", "Doe")]
    [InlineData("John", "Doe", "John Doe")]
    [InlineData("  John  ", "  Doe  ", "John Doe")]
    public void DisplayName_ShouldCombineFirstAndLastName(string firstName, string lastName, string expected)
    {
        // Arrange
        var user = new User
        {
            FirstName = firstName,
            LastName = lastName
        };

        // Act
        var displayName = user.DisplayName;

        // Assert
        displayName.Should().Be(expected.Trim());
    }

    [Fact]
    public void User_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var user = new User();

        // Assert
        user.Id.Should().Be(0);
        user.Handle.Should().Be(string.Empty);
        user.PasswordHash.Should().Be(string.Empty);
        user.Salt.Should().Be(string.Empty);
        user.SecurityLevel.Should().Be(SecurityLevel.User);
        user.IsActive.Should().BeTrue();
        user.FailedLoginAttempts.Should().Be(0);
        user.IsLocked.Should().BeFalse();
        user.IsPasswordExpired.Should().BeFalse();
    }
}
