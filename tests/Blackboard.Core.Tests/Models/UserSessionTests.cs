using Blackboard.Core.Models;

namespace Blackboard.Core.Tests.Models;

public class UserSessionTests
{
    [Fact]
    public void IsExpired_WhenExpiresAtIsFuture_ShouldReturnFalse()
    {
        // Arrange
        var session = new UserSession
        {
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        // Act & Assert
        session.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_WhenExpiresAtIsPast_ShouldReturnTrue()
    {
        // Arrange
        var session = new UserSession
        {
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        };

        // Act & Assert
        session.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WhenActiveAndNotExpired_ShouldReturnTrue()
    {
        // Arrange
        var session = new UserSession
        {
            IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        // Act & Assert
        session.IsValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WhenInactive_ShouldReturnFalse()
    {
        // Arrange
        var session = new UserSession
        {
            IsActive = false,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        // Act & Assert
        session.IsValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenExpired_ShouldReturnFalse()
    {
        // Arrange
        var session = new UserSession
        {
            IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        };

        // Act & Assert
        session.IsValid.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenInactiveAndExpired_ShouldReturnFalse()
    {
        // Arrange
        var session = new UserSession
        {
            IsActive = false,
            ExpiresAt = DateTime.UtcNow.AddHours(-1)
        };

        // Act & Assert
        session.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UserSession_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var session = new UserSession();

        // Assert
        session.Id.Should().Be(string.Empty);
        session.UserId.Should().Be(0);
        session.IpAddress.Should().Be(string.Empty);
        session.UserAgent.Should().BeNull();
        session.IsActive.Should().BeTrue();
        session.CreatedAt.Should().Be(default);
        session.ExpiresAt.Should().Be(default);
    }
}
