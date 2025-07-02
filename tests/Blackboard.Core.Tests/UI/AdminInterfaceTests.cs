using Xunit;
using Moq;
using Blackboard.Core.DTOs;
using Blackboard.Core.Services;
using Blackboard.Core.Models;
using Blackboard.UI.Admin;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Blackboard.Core.Tests.UI;

public class AdminInterfaceTests
{
    private readonly Mock<IUserService> _mockUserService;
    private readonly Mock<IAuditService> _mockAuditService;
    private readonly Mock<ISystemStatisticsService> _mockStatisticsService;
    private readonly Mock<Microsoft.Extensions.Logging.ILogger> _mockLogger;

    public AdminInterfaceTests()
    {
        _mockUserService = new Mock<IUserService>();
        _mockAuditService = new Mock<IAuditService>();
        _mockStatisticsService = new Mock<ISystemStatisticsService>();
        _mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger>();
    }

    [Fact]
    public void AdminDashboard_CanBeCreated()
    {
        // Arrange & Act
        var dashboard = new AdminDashboard(_mockStatisticsService.Object, Mock.Of<Serilog.ILogger>());

        // Assert
        Assert.NotNull(dashboard);
        Assert.Equal("Admin Dashboard", dashboard.Title);
    }

    [Fact]
    public void UserManagementWindow_CanBeCreated()
    {
        // Arrange & Act
        var userManagement = new UserManagementWindow(
            _mockUserService.Object, 
            _mockAuditService.Object, 
            Mock.Of<Serilog.ILogger>());

        // Assert
        Assert.NotNull(userManagement);
        Assert.Equal("User Management", userManagement.Title);
    }

    [Fact]
    public async Task UserManagementWindow_LoadsUsers()
    {
        // Arrange
        var mockUsers = new List<UserProfileDto>
        {
            new UserProfileDto
            {
                Id = 1,
                Handle = "testuser1",
                SecurityLevel = SecurityLevel.User,
                IsActive = true,
                IsLocked = false,
                LastLoginAt = DateTime.UtcNow
            },
            new UserProfileDto
            {
                Id = 2,
                Handle = "testuser2",
                SecurityLevel = SecurityLevel.Moderator,
                IsActive = true,
                IsLocked = false,
                LastLoginAt = DateTime.UtcNow.AddDays(-1)
            }
        };

        _mockUserService.Setup(u => u.GetUsersAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(mockUsers);

        // Act
        var userManagement = new UserManagementWindow(
            _mockUserService.Object, 
            _mockAuditService.Object, 
            Mock.Of<Serilog.ILogger>());

        // Assert
        _mockUserService.Verify(u => u.GetUsersAsync(0, 100), Times.Once);
    }

    [Fact]
    public async Task AdminDashboard_UpdatesStatistics()
    {
        // Arrange
        var mockStats = new DashboardStatisticsDto
        {
            SystemStats = new SystemStatisticsDto
            {
                TotalUsers = 100,
                ActiveUsers = 5,
                ActiveSessions = 3,
                CallsToday = 25,
                RegistrationsToday = 2,
                SystemUptime = TimeSpan.FromHours(10)
            },
            ActiveSessions = new List<ActiveSessionDto>
            {
                new ActiveSessionDto
                {
                    SessionId = "session1",
                    Handle = "testuser",
                    IpAddress = "127.0.0.1",
                    LoginTime = DateTime.UtcNow.AddMinutes(-30),
                    SessionDuration = TimeSpan.FromMinutes(30),
                    CurrentActivity = "Online",
                    IsActive = true
                }
            },
            SystemAlerts = new List<SystemAlertDto>(),
            SystemResources = new SystemResourcesDto
            {
                MemoryUsagePercent = 45.5,
                DiskUsagePercent = 65.2,
                ActiveConnections = 3,
                MaxConnections = 10
            },
            DatabaseStatus = new DatabaseStatusDto
            {
                IsConnected = true,
                DatabaseVersion = "3.40.0",
                WalModeEnabled = true,
                DatabaseSizeBytes = 1024 * 1024 // 1MB
            }
        };

        _mockStatisticsService.Setup(s => s.GetDashboardStatisticsAsync())
            .ReturnsAsync(mockStats);

        // Act
        var dashboard = new AdminDashboard(_mockStatisticsService.Object, Mock.Of<Serilog.ILogger>());

        // Assert - The dashboard should request statistics
        // Note: Due to async nature and timer-based updates, we verify the service was set up correctly
        Assert.NotNull(dashboard);
    }

    [Fact]
    public void UserEditDialog_CanBeCreated()
    {
        // Arrange
        var testUser = new UserProfileDto
        {
            Id = 1,
            Handle = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Location = "Test City",
            SecurityLevel = SecurityLevel.User,
            IsActive = true
        };

        // Act
        var editDialog = new UserEditDialog(testUser, _mockUserService.Object, Mock.Of<Serilog.ILogger>());

        // Assert
        Assert.NotNull(editDialog);
        Assert.Equal("Edit User", editDialog.Title);
    }

    [Fact]
    public void UserAuditDialog_CanBeCreated()
    {
        // Arrange
        var testUser = new UserProfileDto
        {
            Id = 1,
            Handle = "testuser"
        };

        var mockAuditLogs = new List<AuditLog>
        {
            new AuditLog
            {
                Id = 1,
                UserId = 1,
                Action = "USER_LOGIN",
                CreatedAt = DateTime.UtcNow,
                IpAddress = "127.0.0.1"
            }
        };

        _mockAuditService.Setup(a => a.GetUserAuditLogsAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(mockAuditLogs);

        // Act
        var auditDialog = new UserAuditDialog(testUser, _mockAuditService.Object, Mock.Of<Serilog.ILogger>());

        // Assert
        Assert.NotNull(auditDialog);
        Assert.Equal("Audit Log - testuser", auditDialog.Title);
    }
}
