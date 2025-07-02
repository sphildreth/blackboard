using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using Blackboard.Core.Services;
using Blackboard.Core.DTOs;
using Blackboard.Data;
using Blackboard.Data.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Blackboard.Core.Tests.Services;

public class SystemStatisticsServiceTests
{
    private readonly Mock<DatabaseManager> _mockDatabase;
    private readonly Mock<IDatabaseConfiguration> _mockConfig;
    private readonly Mock<Microsoft.Extensions.Logging.ILogger> _mockLogger;
    private readonly SystemStatisticsService _service;

    public SystemStatisticsServiceTests()
    {
        _mockDatabase = new Mock<DatabaseManager>();
        _mockConfig = new Mock<IDatabaseConfiguration>();
        _mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger>();
        
        _mockConfig.Setup(c => c.ConnectionString).Returns("Data Source=:memory:");
        
        _service = new SystemStatisticsService(_mockDatabase.Object, _mockConfig.Object, 
            Mock.Of<Serilog.ILogger>());
    }

    [Fact]
    public async Task GetSystemStatisticsAsync_ReturnsValidStatistics()
    {
        // Arrange
        _mockDatabase.Setup(db => db.QueryFirstOrDefaultAsync<int>(
            It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(10); // Mock various counts

        _mockDatabase.Setup(db => db.QueryFirstOrDefaultAsync<DateTime?>(
            It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(DateTime.UtcNow.AddDays(-30));

        // Act
        var result = await _service.GetSystemStatisticsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.SystemUptime.TotalSeconds > 0);
    }

    [Fact]
    public async Task GetActiveSessionsAsync_ReturnsSessionList()
    {
        // Arrange
        var mockSessionData = new[]
        {
            new { SessionId = "session1", Handle = "user1", IpAddress = "127.0.0.1", 
                  LoginTime = DateTime.UtcNow, IsActive = true, 
                  RealName = "Test User", Location = "Test City", UserAgent = "TestAgent" }
        };

        _mockDatabase.Setup(db => db.QueryAsync<dynamic>(
            It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(mockSessionData);

        // Act
        var result = await _service.GetActiveSessionsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        var session = result.First();
        Assert.Equal("session1", session.SessionId);
        Assert.Equal("user1", session.Handle);
        Assert.Equal("127.0.0.1", session.IpAddress);
    }

    [Fact]
    public async Task GetSystemAlertsAsync_ReturnsAlerts()
    {
        // Arrange
        _mockDatabase.Setup(db => db.QueryFirstOrDefaultAsync<int>(
            It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(6); // Mock 6 failed login attempts

        // Act
        var result = await _service.GetSystemAlertsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Contains(result, alert => alert.Type == AlertType.Security);
    }

    [Fact]
    public async Task GetDatabaseStatusAsync_ReturnsStatus()
    {
        // Arrange
        _mockDatabase.Setup(db => db.QueryFirstOrDefaultAsync<int>(It.IsAny<string>()))
            .ReturnsAsync(1); // Mock successful connection test

        _mockDatabase.Setup(db => db.QueryFirstOrDefaultAsync<string>(It.IsAny<string>()))
            .ReturnsAsync("3.40.0"); // Mock SQLite version

        // Act
        var result = await _service.GetDatabaseStatusAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsConnected);
        Assert.Equal("3.40.0", result.DatabaseVersion);
    }

    [Fact]
    public async Task GetSystemResourcesAsync_ReturnsResourceInfo()
    {
        // Arrange
        _mockDatabase.Setup(db => db.QueryFirstOrDefaultAsync<int>(
            It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(2); // Mock active connections

        // Act
        var result = await _service.GetSystemResourcesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.MemoryUsagePercent >= 0);
        Assert.True(result.DiskUsagePercent >= 0);
        Assert.Equal(2, result.ActiveConnections);
    }

    [Fact]
    public async Task GetDashboardStatisticsAsync_ReturnsCompleteData()
    {
        // Arrange
        _mockDatabase.Setup(db => db.QueryFirstOrDefaultAsync<int>(
            It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(5);

        _mockDatabase.Setup(db => db.QueryAsync<dynamic>(
            It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(new dynamic[0]);

        // Act
        var result = await _service.GetDashboardStatisticsAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.SystemStats);
        Assert.NotNull(result.ActiveSessions);
        Assert.NotNull(result.SystemAlerts);
        Assert.NotNull(result.SystemResources);
        Assert.NotNull(result.DatabaseStatus);
    }
}
