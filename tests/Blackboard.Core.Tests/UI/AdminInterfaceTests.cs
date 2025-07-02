using Xunit;
using Moq;
using Blackboard.Core.DTOs;
using Blackboard.Core.Services;
using Blackboard.Core.Models;
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
    public void AdminServices_CanBeCreated()
    {
        // Test that the admin services can be instantiated
        Assert.NotNull(_mockUserService.Object);
        Assert.NotNull(_mockAuditService.Object);
        Assert.NotNull(_mockStatisticsService.Object);
    }

    [Fact]
    public async Task UserService_CanGetUsers()
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
            }
        };

        _mockUserService.Setup(u => u.GetUsersAsync(It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(mockUsers);

        // Act
        var result = await _mockUserService.Object.GetUsersAsync(0, 100);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("testuser1", result.First().Handle);
    }

    [Fact]
    public async Task StatisticsService_CanGetDashboardData()
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
        var result = await _mockStatisticsService.Object.GetDashboardStatisticsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.SystemStats);
        Assert.Equal(100, result.SystemStats.TotalUsers);
        Assert.Equal(5, result.SystemStats.ActiveUsers);
        Assert.Single(result.ActiveSessions);
        Assert.True(result.DatabaseStatus.IsConnected);
    }

    [Fact]
    public async Task AuditService_CanGetUserLogs()
    {
        // Arrange
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
        var result = await _mockAuditService.Object.GetUserAuditLogsAsync(1, 50);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("USER_LOGIN", result.First().Action);
        Assert.Equal("127.0.0.1", result.First().IpAddress);
    }

    [Fact]
    public void UserProfileDto_CanBeCreated()
    {
        // Arrange & Act
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

        // Assert
        Assert.Equal(1, testUser.Id);
        Assert.Equal("testuser", testUser.Handle);
        Assert.Equal("test@example.com", testUser.Email);
        Assert.Equal(SecurityLevel.User, testUser.SecurityLevel);
        Assert.True(testUser.IsActive);
    }
}
