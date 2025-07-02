using Blackboard.Core.Models;

namespace Blackboard.Core.Tests.Models;

public class SecurityLevelTests
{
    [Theory]
    [InlineData(SecurityLevel.Banned, -1)]
    [InlineData(SecurityLevel.User, 0)]
    [InlineData(SecurityLevel.Trusted, 10)]
    [InlineData(SecurityLevel.Moderator, 50)]
    [InlineData(SecurityLevel.CoSysop, 90)]
    [InlineData(SecurityLevel.Sysop, 100)]
    public void SecurityLevel_ShouldHaveCorrectNumericValues(SecurityLevel level, int expectedValue)
    {
        // Act
        var numericValue = (int)level;

        // Assert
        numericValue.Should().Be(expectedValue);
    }

    [Fact]
    public void SecurityLevel_ShouldAllowComparison()
    {
        // Assert
        ((int)SecurityLevel.Sysop).Should().BeGreaterThan((int)SecurityLevel.CoSysop);
        ((int)SecurityLevel.CoSysop).Should().BeGreaterThan((int)SecurityLevel.Moderator);
        ((int)SecurityLevel.Moderator).Should().BeGreaterThan((int)SecurityLevel.Trusted);
        ((int)SecurityLevel.Trusted).Should().BeGreaterThan((int)SecurityLevel.User);
        ((int)SecurityLevel.User).Should().BeGreaterThan((int)SecurityLevel.Banned);
    }

    [Fact]
    public void SecurityLevel_ShouldSupportEqualityComparison()
    {
        // Arrange
        var level1 = SecurityLevel.Moderator;
        var level2 = SecurityLevel.Moderator;
        var level3 = SecurityLevel.User;

        // Assert
        level1.Should().Be(level2);
        level1.Should().NotBe(level3);
    }
}
