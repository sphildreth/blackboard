using Blackboard.Core.Models;
using Blackboard.Core.Services;

namespace Blackboard.Core.Tests.Services;

public class AuthorizationServiceTests
{
    private readonly IAuthorizationService _authorizationService;

    public AuthorizationServiceTests()
    {
        _authorizationService = new AuthorizationService();
    }

    [Theory]
    [InlineData(SecurityLevel.User, SecurityLevel.User, true)]
    [InlineData(SecurityLevel.Trusted, SecurityLevel.User, true)]
    [InlineData(SecurityLevel.Moderator, SecurityLevel.User, true)]
    [InlineData(SecurityLevel.CoSysop, SecurityLevel.Moderator, true)]
    [InlineData(SecurityLevel.Sysop, SecurityLevel.CoSysop, true)]
    [InlineData(SecurityLevel.User, SecurityLevel.Trusted, false)]
    [InlineData(SecurityLevel.User, SecurityLevel.Moderator, false)]
    [InlineData(SecurityLevel.Trusted, SecurityLevel.Moderator, false)]
    [InlineData(SecurityLevel.Banned, SecurityLevel.User, false)]
    public void HasPermission_ShouldReturnExpectedResult(SecurityLevel userLevel, SecurityLevel requiredLevel, bool expected)
    {
        // Act
        var result = _authorizationService.HasPermission(userLevel, requiredLevel);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(SecurityLevel.Banned, false)]
    [InlineData(SecurityLevel.User, false)]
    [InlineData(SecurityLevel.Trusted, false)]
    [InlineData(SecurityLevel.Moderator, true)]
    [InlineData(SecurityLevel.CoSysop, true)]
    [InlineData(SecurityLevel.Sysop, true)]
    public void CanAccessAdminPanel_ShouldReturnExpectedResult(SecurityLevel userLevel, bool expected)
    {
        // Act
        var result = _authorizationService.CanAccessAdminPanel(userLevel);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(SecurityLevel.Banned, false)]
    [InlineData(SecurityLevel.User, false)]
    [InlineData(SecurityLevel.Trusted, false)]
    [InlineData(SecurityLevel.Moderator, false)]
    [InlineData(SecurityLevel.CoSysop, true)]
    [InlineData(SecurityLevel.Sysop, true)]
    public void CanManageUsers_ShouldReturnExpectedResult(SecurityLevel userLevel, bool expected)
    {
        // Act
        var result = _authorizationService.CanManageUsers(userLevel);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(SecurityLevel.Banned, false)]
    [InlineData(SecurityLevel.User, false)]
    [InlineData(SecurityLevel.Trusted, false)]
    [InlineData(SecurityLevel.Moderator, true)]
    [InlineData(SecurityLevel.CoSysop, true)]
    [InlineData(SecurityLevel.Sysop, true)]
    public void CanViewAuditLogs_ShouldReturnExpectedResult(SecurityLevel userLevel, bool expected)
    {
        // Act
        var result = _authorizationService.CanViewAuditLogs(userLevel);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(SecurityLevel.Banned, false)]
    [InlineData(SecurityLevel.User, false)]
    [InlineData(SecurityLevel.Trusted, false)]
    [InlineData(SecurityLevel.Moderator, true)]
    [InlineData(SecurityLevel.CoSysop, true)]
    [InlineData(SecurityLevel.Sysop, true)]
    public void CanModerateContent_ShouldReturnExpectedResult(SecurityLevel userLevel, bool expected)
    {
        // Act
        var result = _authorizationService.CanModerateContent(userLevel);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(SecurityLevel.Banned, false)]
    [InlineData(SecurityLevel.User, false)]
    [InlineData(SecurityLevel.Trusted, false)]
    [InlineData(SecurityLevel.Moderator, false)]
    [InlineData(SecurityLevel.CoSysop, false)]
    [InlineData(SecurityLevel.Sysop, true)]
    public void CanManageSystem_ShouldReturnExpectedResult(SecurityLevel userLevel, bool expected)
    {
        // Act
        var result = _authorizationService.CanManageSystem(userLevel);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(SecurityLevel.Banned, null, false)]
    [InlineData(SecurityLevel.User, null, true)]
    [InlineData(SecurityLevel.Trusted, null, true)]
    [InlineData(SecurityLevel.User, SecurityLevel.Trusted, false)]
    [InlineData(SecurityLevel.Trusted, SecurityLevel.Trusted, true)]
    [InlineData(SecurityLevel.Moderator, SecurityLevel.Trusted, true)]
    public void CanAccessFileAreas_ShouldReturnExpectedResult(SecurityLevel userLevel, SecurityLevel? areaMinLevel, bool expected)
    {
        // Act
        var result = _authorizationService.CanAccessFileAreas(userLevel, areaMinLevel);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(SecurityLevel.Banned, false)]
    [InlineData(SecurityLevel.User, false)]
    [InlineData(SecurityLevel.Trusted, true)]
    [InlineData(SecurityLevel.Moderator, true)]
    [InlineData(SecurityLevel.CoSysop, true)]
    [InlineData(SecurityLevel.Sysop, true)]
    public void CanUploadFiles_ShouldReturnExpectedResult(SecurityLevel userLevel, bool expected)
    {
        // Act
        var result = _authorizationService.CanUploadFiles(userLevel);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(SecurityLevel.Banned, false)]
    [InlineData(SecurityLevel.User, true)]
    [InlineData(SecurityLevel.Trusted, true)]
    [InlineData(SecurityLevel.Moderator, true)]
    [InlineData(SecurityLevel.CoSysop, true)]
    [InlineData(SecurityLevel.Sysop, true)]
    public void CanDownloadFiles_ShouldReturnExpectedResult(SecurityLevel userLevel, bool expected)
    {
        // Act
        var result = _authorizationService.CanDownloadFiles(userLevel);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(SecurityLevel.Banned, false)]
    [InlineData(SecurityLevel.User, true)]
    [InlineData(SecurityLevel.Trusted, true)]
    [InlineData(SecurityLevel.Moderator, true)]
    [InlineData(SecurityLevel.CoSysop, true)]
    [InlineData(SecurityLevel.Sysop, true)]
    public void CanSendPrivateMessages_ShouldReturnExpectedResult(SecurityLevel userLevel, bool expected)
    {
        // Act
        var result = _authorizationService.CanSendPrivateMessages(userLevel);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(SecurityLevel.Banned, false)]
    [InlineData(SecurityLevel.User, true)]
    [InlineData(SecurityLevel.Trusted, true)]
    [InlineData(SecurityLevel.Moderator, true)]
    [InlineData(SecurityLevel.CoSysop, true)]
    [InlineData(SecurityLevel.Sysop, true)]
    public void CanPostPublicMessages_ShouldReturnExpectedResult(SecurityLevel userLevel, bool expected)
    {
        // Act
        var result = _authorizationService.CanPostPublicMessages(userLevel);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(SecurityLevel.Banned, false)]
    [InlineData(SecurityLevel.User, true)]
    [InlineData(SecurityLevel.Trusted, true)]
    [InlineData(SecurityLevel.Moderator, true)]
    [InlineData(SecurityLevel.CoSysop, true)]
    [InlineData(SecurityLevel.Sysop, true)]
    public void CanAccessDoorGames_ShouldReturnExpectedResult(SecurityLevel userLevel, bool expected)
    {
        // Act
        var result = _authorizationService.CanAccessDoorGames(userLevel);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(SecurityLevel.Banned, false)]
    [InlineData(SecurityLevel.User, false)]
    [InlineData(SecurityLevel.Trusted, false)]
    [InlineData(SecurityLevel.Moderator, true)]
    [InlineData(SecurityLevel.CoSysop, true)]
    [InlineData(SecurityLevel.Sysop, true)]
    public void CanKickUsers_ShouldReturnExpectedResult(SecurityLevel userLevel, bool expected)
    {
        // Act
        var result = _authorizationService.CanKickUsers(userLevel);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(SecurityLevel.Banned, false)]
    [InlineData(SecurityLevel.User, false)]
    [InlineData(SecurityLevel.Trusted, false)]
    [InlineData(SecurityLevel.Moderator, false)]
    [InlineData(SecurityLevel.CoSysop, true)]
    [InlineData(SecurityLevel.Sysop, true)]
    public void CanBanUsers_ShouldReturnExpectedResult(SecurityLevel userLevel, bool expected)
    {
        // Act
        var result = _authorizationService.CanBanUsers(userLevel);

        // Assert
        result.Should().Be(expected);
    }
}
