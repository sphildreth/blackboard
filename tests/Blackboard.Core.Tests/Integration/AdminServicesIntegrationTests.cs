using Xunit;
using Microsoft.Data.Sqlite;
using Blackboard.Core.Services;
using Blackboard.Core.Configuration;
using Blackboard.Data;
using Blackboard.Data.Configuration;
using Serilog;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Blackboard.Core.Tests.Integration;

public class AdminServicesIntegrationTests : IDisposable
{
    private readonly DatabaseManager _database;
    private readonly SqliteConnection _connection;
    private readonly ILogger _logger;
    private readonly SystemStatisticsService _statisticsService;
    private readonly UserService _userService;
    private readonly AuditService _auditService;

    public AdminServicesIntegrationTests()
    {
        // Setup in-memory database
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        
        _logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();

        var dbConfig = new DatabaseConfiguration
        {
            ConnectionString = _connection.ConnectionString,
            EnableWalMode = false
        };

        _database = new DatabaseManager(_logger, dbConfig);
        _database.InitializeAsync().Wait();

        var securitySettings = new SecuritySettings
        {
            MaxLoginAttempts = 3,
            LockoutDurationMinutes = 30,
            PasswordMinLength = 8,
            RequirePasswordComplexity = true,
            PasswordExpirationDays = 90
        };

        var passwordService = new PasswordService();
        var sessionService = new SessionService(_database, _logger);
        _auditService = new AuditService(_database, _logger);
        _userService = new UserService(_database, passwordService, sessionService, 
            _auditService, securitySettings, _logger);
        _statisticsService = new SystemStatisticsService(_database, dbConfig, _logger);
    }

    [Fact]
    public async Task AdminServices_CanCreateAndManageUsers()
    {
        // Arrange
        var registrationDto = new UserRegistrationDto
        {
            Handle = "testadmin",
            Email = "admin@test.com",
            Password = "TestPassword123!",
            FirstName = "Test",
            LastName = "Admin",
            Location = "Test City"
        };

        // Act - Register user
        var user = await _userService.RegisterUserAsync(registrationDto, "127.0.0.1", "TestAgent");

        // Assert
        Assert.NotNull(user);
        Assert.Equal("testadmin", user.Handle);

        // Act - Get statistics
        var stats = await _statisticsService.GetSystemStatisticsAsync();

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(1, stats.TotalUsers);
        Assert.Equal(1, stats.RegistrationsToday);
    }

    [Fact]
    public async Task AdminServices_CanTrackUserActivity()
    {
        // Arrange
        var registrationDto = new UserRegistrationDto
        {
            Handle = "testuser",
            Email = "user@test.com",
            Password = "TestPassword123!",
            FirstName = "Test",
            LastName = "User"
        };

        var user = await _userService.RegisterUserAsync(registrationDto, "127.0.0.1");

        // Act - Login user
        var loginDto = new UserLoginDto
        {
            Handle = "testuser",
            Password = "TestPassword123!",
            IpAddress = "127.0.0.1",
            UserAgent = "TestAgent"
        };

        var loginResult = await _userService.LoginAsync(loginDto);

        // Assert
        Assert.NotNull(loginResult.User);
        Assert.NotNull(loginResult.Session);

        // Act - Get active sessions
        var activeSessions = await _statisticsService.GetActiveSessionsAsync();

        // Assert
        Assert.NotNull(activeSessions);
        Assert.Single(activeSessions);
        Assert.Equal("testuser", activeSessions.First().Handle);
        Assert.Equal("127.0.0.1", activeSessions.First().IpAddress);
    }

    [Fact]
    public async Task AdminServices_CanGenerateAuditLogs()
    {
        // Arrange
        var registrationDto = new UserRegistrationDto
        {
            Handle = "audituser",
            Password = "TestPassword123!",
            Email = "audit@test.com"
        };

        var user = await _userService.RegisterUserAsync(registrationDto, "127.0.0.1");

        // Act - Perform various operations
        await _userService.LockUserAsync(user!.Id, TimeSpan.FromHours(1), "Test lock", 1, "127.0.0.1");
        await _userService.UnlockUserAsync(user.Id, 1, "127.0.0.1");

        // Act - Get audit logs
        var auditLogs = await _auditService.GetUserAuditLogsAsync(user.Id);

        // Assert
        Assert.NotNull(auditLogs);
        Assert.True(auditLogs.Count() >= 2); // At least registration, lock, unlock
        Assert.Contains(auditLogs, log => log.Action == "USER_REGISTRATION");
        Assert.Contains(auditLogs, log => log.Action == "USER_LOCKED");
        Assert.Contains(auditLogs, log => log.Action == "USER_UNLOCKED");
    }

    [Fact]
    public async Task AdminServices_CanDetectSecurityAlerts()
    {
        // Arrange - Create multiple failed login attempts
        for (int i = 0; i < 6; i++)
        {
            await _auditService.LogAsync("USER_LOGIN_FAILED", null, null, null, null, null, "127.0.0.1");
        }

        // Act
        var alerts = await _statisticsService.GetSystemAlertsAsync();

        // Assert
        Assert.NotNull(alerts);
        Assert.Contains(alerts, alert => alert.Type == AlertType.Security && 
                                       alert.Title.Contains("Failed Login"));
    }

    [Fact]
    public async Task AdminServices_CanGetDashboardData()
    {
        // Arrange - Create some test data
        var user1 = await _userService.RegisterUserAsync(new UserRegistrationDto
        {
            Handle = "user1",
            Password = "TestPassword123!",
            Email = "user1@test.com"
        }, "127.0.0.1");

        var user2 = await _userService.RegisterUserAsync(new UserRegistrationDto
        {
            Handle = "user2", 
            Password = "TestPassword123!",
            Email = "user2@test.com"
        }, "192.168.1.1");

        // Login one user
        await _userService.LoginAsync(new UserLoginDto
        {
            Handle = "user1",
            Password = "TestPassword123!",
            IpAddress = "127.0.0.1"
        });

        // Act
        var dashboardData = await _statisticsService.GetDashboardStatisticsAsync();

        // Assert
        Assert.NotNull(dashboardData);
        Assert.NotNull(dashboardData.SystemStats);
        Assert.NotNull(dashboardData.ActiveSessions);
        Assert.NotNull(dashboardData.SystemResources);
        Assert.NotNull(dashboardData.DatabaseStatus);

        Assert.Equal(2, dashboardData.SystemStats.TotalUsers);
        Assert.Equal(1, dashboardData.SystemStats.ActiveSessions);
        Assert.Single(dashboardData.ActiveSessions);
        Assert.True(dashboardData.DatabaseStatus.IsConnected);
    }

    public void Dispose()
    {
        _database?.Dispose();
        _connection?.Dispose();
    }
}
