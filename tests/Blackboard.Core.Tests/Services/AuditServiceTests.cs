using Blackboard.Core.Services;
using Blackboard.Core.Tests.Helpers;
using Serilog;

namespace Blackboard.Core.Tests.Services;

public class AuditServiceTests : IAsyncLifetime
{
    private readonly TestDatabaseHelper _databaseHelper;
    private readonly IAuditService _auditService;
    private readonly ILogger _logger;

    public AuditServiceTests()
    {
        _databaseHelper = new TestDatabaseHelper();
        _logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();
        _auditService = new AuditService(_databaseHelper.DatabaseManager, _logger);
    }

    public async Task InitializeAsync()
    {
        await _databaseHelper.InitializeAsync();
        // Create default test users for audit tests
        await TestDataHelper.CreateTestUserAsync(_databaseHelper.DatabaseManager, "testuser1", 1);
        await TestDataHelper.CreateTestUserAsync(_databaseHelper.DatabaseManager, "testuser2", 2);
        await TestDataHelper.CreateTestUserAsync(_databaseHelper.DatabaseManager, "testuser3", 3);
    }

    public Task DisposeAsync()
    {
        _databaseHelper.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task LogAsync_WithBasicData_ShouldCreateAuditLog()
    {
        // Arrange
        var action = "TEST_ACTION";
        var userId = 1;
        var ipAddress = "192.168.1.100";

        // Act
        await _auditService.LogAsync(action, userId, ipAddress: ipAddress);

        // Assert
        var logs = await _auditService.GetAuditLogsAsync(userId: userId, limit: 1);
        logs.Should().HaveCount(1);

        var log = logs.First();
        log.Action.Should().Be(action);
        log.UserId.Should().Be(userId);
        log.IpAddress.Should().Be(ipAddress);
        log.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task LogAsync_WithComplexData_ShouldSerializeCorrectly()
    {
        // Arrange
        var action = "USER_UPDATED";
        var userId = 1;
        var entityType = "User";
        var entityId = "123";
        var oldValues = new { Name = "Old Name", Email = "old@test.com" };
        var newValues = new { Name = "New Name", Email = "new@test.com" };

        // Act
        await _auditService.LogAsync(action, userId, entityType, entityId, oldValues, newValues);

        // Assert
        var logs = await _auditService.GetAuditLogsAsync(userId: userId, limit: 1);
        logs.Should().HaveCount(1);

        var log = logs.First();
        log.Action.Should().Be(action);
        log.EntityType.Should().Be(entityType);
        log.EntityId.Should().Be(entityId);
        log.OldValues.Should().NotBeNullOrEmpty();
        log.NewValues.Should().NotBeNullOrEmpty();
        log.OldValues.Should().Contain("Old Name");
        log.NewValues.Should().Contain("New Name");
    }

    [Fact]
    public async Task LogAsync_WithNullUserId_ShouldCreateLogWithoutUserReference()
    {
        // Arrange
        var action = "SYSTEM_ACTION";

        // Act
        await _auditService.LogAsync(action, userId: null);

        // Assert
        var logs = await _auditService.GetAuditLogsAsync(limit: 1);
        logs.Should().HaveCount(1);

        var log = logs.First();
        log.Action.Should().Be(action);
        log.UserId.Should().BeNull();
    }

    [Fact]
    public async Task LogUserActionAsync_ShouldCreateUserSpecificLog()
    {
        // Arrange
        var userId = 1;
        var action = "USER_LOGIN";
        var ipAddress = "192.168.1.100";
        var userAgent = "Test Browser";

        // Act
        await _auditService.LogUserActionAsync(userId, action, ipAddress, userAgent);

        // Assert
        var logs = await _auditService.GetUserAuditLogsAsync(userId, limit: 1);
        logs.Should().HaveCount(1);

        var log = logs.First();
        log.Action.Should().Be(action);
        log.UserId.Should().Be(userId);
        log.EntityType.Should().Be("User");
        log.EntityId.Should().Be(userId.ToString());
        log.IpAddress.Should().Be(ipAddress);
        log.UserAgent.Should().Be(userAgent);
    }

    [Fact]
    public async Task LogEntityChangeAsync_ShouldCreateEntityChangeLog()
    {
        // Arrange
        var userId = 1;
        var action = "ENTITY_UPDATED";
        var entityId = "test-entity-123";
        var oldData = new { Status = "Active", Count = 5 };
        var newData = new { Status = "Inactive", Count = 10 };

        // Act
        await _auditService.LogEntityChangeAsync(userId, action, entityId, oldData, newData);

        // Assert
        var logs = await _auditService.GetAuditLogsAsync(userId: userId, limit: 1);
        logs.Should().HaveCount(1);

        var log = logs.First();
        log.Action.Should().Be(action);
        log.EntityType.Should().Contain("AnonymousType"); // Type name will contain this
        log.EntityId.Should().Be(entityId);
        log.OldValues.Should().Contain("Active");
        log.NewValues.Should().Contain("Inactive");
    }

    [Fact]
    public async Task GetAuditLogsAsync_WithNoFilters_ShouldReturnAllLogs()
    {
        // Arrange
        await _auditService.LogAsync("ACTION_1", 1);
        await _auditService.LogAsync("ACTION_2", 2);
        await _auditService.LogAsync("ACTION_3", 1);

        // Act
        var logs = await _auditService.GetAuditLogsAsync();

        // Assert
        logs.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAuditLogsAsync_WithUserIdFilter_ShouldReturnUserLogs()
    {
        // Arrange
        await _auditService.LogAsync("ACTION_1", 1);
        await _auditService.LogAsync("ACTION_2", 2);
        await _auditService.LogAsync("ACTION_3", 1);

        // Act
        var logs = await _auditService.GetAuditLogsAsync(userId: 1);

        // Assert
        logs.Should().HaveCount(2);
        logs.Should().OnlyContain(log => log.UserId == 1);
    }

    [Fact]
    public async Task GetAuditLogsAsync_WithDateFilters_ShouldReturnLogsInRange()
    {
        // Arrange
        var yesterday = DateTime.UtcNow.AddDays(-1);
        var tomorrow = DateTime.UtcNow.AddDays(1);

        await _auditService.LogAsync("ACTION_TODAY", 1);

        // Act
        var logs = await _auditService.GetAuditLogsAsync(fromDate: yesterday, toDate: tomorrow);

        // Assert
        logs.Should().HaveCount(1);
        logs.First().Action.Should().Be("ACTION_TODAY");
    }

    [Fact]
    public async Task GetAuditLogsAsync_WithDateFiltersExcludingToday_ShouldReturnNoLogs()
    {
        // Arrange
        var dayBeforeYesterday = DateTime.UtcNow.AddDays(-2);
        var yesterday = DateTime.UtcNow.AddDays(-1);

        await _auditService.LogAsync("ACTION_TODAY", 1);

        // Act
        var logs = await _auditService.GetAuditLogsAsync(fromDate: dayBeforeYesterday, toDate: yesterday);

        // Assert
        logs.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAuditLogsAsync_WithLimit_ShouldRespectLimit()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            await _auditService.LogAsync($"ACTION_{i}", 1);
        }

        // Act
        var logs = await _auditService.GetAuditLogsAsync(limit: 5);

        // Assert
        logs.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetUserAuditLogsAsync_ShouldReturnOnlyUserLogs()
    {
        // Arrange
        var userId = 1;
        await _auditService.LogAsync("USER_1_ACTION_1", userId);
        await _auditService.LogAsync("USER_1_ACTION_2", userId);
        await _auditService.LogAsync("USER_2_ACTION", 2);

        // Act
        var logs = await _auditService.GetUserAuditLogsAsync(userId);

        // Assert
        logs.Should().HaveCount(2);
        logs.Should().OnlyContain(log => log.UserId == userId);
        logs.Should().Contain(log => log.Action == "USER_1_ACTION_1");
        logs.Should().Contain(log => log.Action == "USER_1_ACTION_2");
    }

    [Fact]
    public async Task GetUserAuditLogsAsync_WithLimit_ShouldRespectLimit()
    {
        // Arrange
        var userId = 1;
        for (int i = 0; i < 10; i++)
        {
            await _auditService.LogAsync($"USER_ACTION_{i}", userId);
        }

        // Act
        var logs = await _auditService.GetUserAuditLogsAsync(userId, limit: 3);

        // Assert
        logs.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAuditLogsAsync_ShouldReturnLogsInDescendingOrderByCreatedAt()
    {
        // Arrange
        await _auditService.LogAsync("FIRST_ACTION", 1);
        await Task.Delay(10); // Small delay to ensure different timestamps
        await _auditService.LogAsync("SECOND_ACTION", 1);
        await Task.Delay(10);
        await _auditService.LogAsync("THIRD_ACTION", 1);

        // Act
        var logs = await _auditService.GetAuditLogsAsync(userId: 1);

        // Assert
        logs.Should().HaveCount(3);
        var logList = logs.ToList();
        logList[0].Action.Should().Be("THIRD_ACTION");  // Most recent first
        logList[1].Action.Should().Be("SECOND_ACTION");
        logList[2].Action.Should().Be("FIRST_ACTION");   // Oldest last
    }
}
