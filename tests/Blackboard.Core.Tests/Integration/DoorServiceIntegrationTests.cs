using Blackboard.Core.DTOs;
using Blackboard.Core.Services;
using Blackboard.Data;
using Blackboard.Data.Configuration;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Serilog;
using Moq;
using Xunit;

namespace Blackboard.Core.Tests.Integration;

public class DoorServiceIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IDatabaseManager _databaseManager;
    private readonly Mock<ILogger> _mockLogger;
    private readonly DoorService _doorService;
    private readonly string _testBasePath;
    private readonly string _testDropFilePath;

    public DoorServiceIntegrationTests()
    {
        // Create in-memory SQLite database
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        
        // Create mock logger
        _mockLogger = new Mock<ILogger>();
        
        // Create database configuration
        var config = new DatabaseConfiguration 
        { 
            ConnectionString = _connection.ConnectionString 
        };
        
        _databaseManager = new DatabaseManager(_mockLogger.Object, config);
        
        // Initialize the database manager
        _databaseManager.InitializeAsync().Wait();
        
        _testBasePath = Path.Combine(Path.GetTempPath(), "blackboard_integration_test_doors");
        _testDropFilePath = Path.Combine(Path.GetTempPath(), "blackboard_integration_test_dropfiles");
        
        _doorService = new DoorService(
            _databaseManager,
            _mockLogger.Object,
            _testBasePath,
            _testDropFilePath
        );

        // Ensure test directories exist
        Directory.CreateDirectory(_testBasePath);
        Directory.CreateDirectory(_testDropFilePath);

        // Initialize database schema
        InitializeDatabaseSchema();
    }

    public void Dispose()
    {
        _connection?.Dispose();
        
        // Clean up test directories
        try
        {
            if (Directory.Exists(_testBasePath))
                Directory.Delete(_testBasePath, true);
            if (Directory.Exists(_testDropFilePath))
                Directory.Delete(_testDropFilePath, true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private void InitializeDatabaseSchema()
    {
        var createTablesSql = @"
            CREATE TABLE IF NOT EXISTS Doors (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL UNIQUE,
                Description TEXT,
                Category TEXT NOT NULL,
                ExecutablePath TEXT NOT NULL,
                CommandLine TEXT,
                WorkingDirectory TEXT,
                DropFileType TEXT NOT NULL DEFAULT 'DOOR.SYS',
                DropFileLocation TEXT,
                IsActive INTEGER NOT NULL DEFAULT 1,
                RequiresDosBox INTEGER NOT NULL DEFAULT 0,
                DosBoxConfigPath TEXT,
                SerialPort TEXT DEFAULT 'COM1',
                MemorySize INTEGER DEFAULT 16,
                MinimumLevel INTEGER DEFAULT 0,
                MaximumLevel INTEGER DEFAULT 255,
                TimeLimit INTEGER DEFAULT 60,
                DailyLimit INTEGER DEFAULT 5,
                Cost INTEGER DEFAULT 0,
                SchedulingEnabled INTEGER DEFAULT 0,
                AvailableHours TEXT,
                TimeZone TEXT DEFAULT 'UTC',
                MultiNodeEnabled INTEGER DEFAULT 0,
                MaxPlayers INTEGER DEFAULT 1,
                InterBbsEnabled INTEGER DEFAULT 0,
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                CreatedBy INTEGER
            );

            CREATE TABLE IF NOT EXISTS DoorSessions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SessionId TEXT NOT NULL UNIQUE,
                DoorId INTEGER NOT NULL,
                UserId INTEGER NOT NULL,
                NodeNumber INTEGER,
                StartTime DATETIME DEFAULT CURRENT_TIMESTAMP,
                EndTime DATETIME,
                ExitCode INTEGER,
                DropFilePath TEXT,
                WorkingDirectory TEXT,
                ProcessId INTEGER,
                Status TEXT NOT NULL DEFAULT 'starting',
                ErrorMessage TEXT,
                LastActivity DATETIME DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (DoorId) REFERENCES Doors(Id)
            );

            CREATE TABLE IF NOT EXISTS DoorConfigs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DoorId INTEGER NOT NULL,
                ConfigKey TEXT NOT NULL,
                ConfigValue TEXT NOT NULL,
                ConfigType TEXT DEFAULT 'string',
                FOREIGN KEY (DoorId) REFERENCES Doors(Id),
                UNIQUE(DoorId, ConfigKey)
            );

            CREATE TABLE IF NOT EXISTS DoorPermissions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DoorId INTEGER NOT NULL,
                UserId INTEGER,
                UserGroup TEXT,
                AccessType TEXT NOT NULL,
                GrantedBy INTEGER NOT NULL,
                GrantedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                ExpiresAt DATETIME,
                FOREIGN KEY (DoorId) REFERENCES Doors(Id)
            );

            CREATE TABLE IF NOT EXISTS DoorStatistics (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DoorId INTEGER NOT NULL,
                UserId INTEGER NOT NULL,
                TotalSessions INTEGER DEFAULT 0,
                TotalTime INTEGER DEFAULT 0,
                LastPlayed DATETIME,
                HighScore INTEGER,
                CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                UpdatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (DoorId) REFERENCES Doors(Id),
                UNIQUE(DoorId, UserId)
            );

            CREATE TABLE IF NOT EXISTS DoorLogs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                DoorId INTEGER NOT NULL,
                SessionId INTEGER,
                LogLevel TEXT NOT NULL,
                Message TEXT NOT NULL,
                Details TEXT,
                Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (DoorId) REFERENCES Doors(Id)
            );

            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Handle TEXT NOT NULL UNIQUE,
                RealName TEXT,
                Location TEXT,
                SecurityLevel INTEGER DEFAULT 10,
                TimeLeft INTEGER DEFAULT 60,
                LastLoginAt DATETIME
            );
        ";

        _databaseManager.ExecuteAsync(createTablesSql).Wait();

        // Insert test user
        var insertUserSql = @"
            INSERT INTO Users (Handle, RealName, Location, SecurityLevel, TimeLeft)
            VALUES ('TestUser', 'Test User', 'Test City', 50, 60)";
        _databaseManager.ExecuteAsync(insertUserSql).Wait();
    }

    #region Full Workflow Integration Tests

    [Fact]
    public async Task FullDoorWorkflow_CreateToSession_WorksCorrectly()
    {
        // 1. Create a door
        var createDto = new CreateDoorDto
        {
            Name = "Test Door Game",
            Description = "A test door game for integration testing",
            Category = "Test",
            ExecutablePath = CreateTestExecutable(),
            CommandLine = "{dropfile}",
            DropFileType = "DOOR.SYS",
            MinimumLevel = 10,
            MaximumLevel = 100,
            TimeLimit = 30,
            DailyLimit = 3
        };

        var door = await _doorService.CreateDoorAsync(createDto, 1);
        door.Should().NotBeNull();
        door.Name.Should().Be(createDto.Name);

        // 2. Check user can access door
        var canAccess = await _doorService.CanUserAccessDoorAsync(1, door.Id);
        canAccess.Should().BeTrue();

        // 3. Check daily limit not reached
        var limitReached = await _doorService.HasUserReachedDailyLimitAsync(1, door.Id);
        limitReached.Should().BeFalse();

        // 4. Start a door session
        var session = await _doorService.StartDoorSessionAsync(door.Id, 1);
        session.Should().NotBeNull();
        session.DoorId.Should().Be(door.Id);
        session.UserId.Should().Be(1);
        session.Status.Should().Be("starting");

        // 5. Generate drop file
        var dropFile = await _doorService.GenerateDropFileAsync(door.Id, 1, session.SessionId);
        dropFile.Should().NotBeNull();
        File.Exists(dropFile.FilePath).Should().BeTrue();

        // 6. Validate drop file
        var isValid = await _doorService.ValidateDropFileAsync(dropFile.FilePath);
        isValid.Should().BeTrue();

        // 7. End the session
        var sessionEnded = await _doorService.EndDoorSessionAsync(session.SessionId, 0);
        sessionEnded.Should().BeTrue();

        // 8. Verify statistics were updated
        var stats = await _doorService.GetUserDoorStatisticsAsync(door.Id, 1);
        stats.Should().NotBeNull();
        stats!.TotalSessions.Should().Be(1);

        // 9. Check session count increased
        var sessionCount = await _doorService.GetUserDailySessionCountAsync(1, door.Id);
        sessionCount.Should().Be(1);
    }

    [Fact]
    public async Task DoorPermissions_DenyAccess_PreventsSession()
    {
        // 1. Create a door
        var createDto = new CreateDoorDto
        {
            Name = "Restricted Door",
            Description = "A door with access restrictions",
            Category = "Test",
            ExecutablePath = CreateTestExecutable(),
            MinimumLevel = 10
        };

        var door = await _doorService.CreateDoorAsync(createDto, 1);

        // 2. Initially user should have access
        var canAccess = await _doorService.CanUserAccessDoorAsync(1, door.Id);
        canAccess.Should().BeTrue();

        // 3. Add deny permission
        await _doorService.AddDoorPermissionAsync(door.Id, 1, null, "deny", 1);

        // 4. Now user should not have access
        canAccess = await _doorService.CanUserAccessDoorAsync(1, door.Id);
        canAccess.Should().BeFalse();

        // 5. Attempting to start session should throw exception
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _doorService.StartDoorSessionAsync(door.Id, 1));
    }

    [Fact]
    public async Task DailyLimit_ReachedLimit_PreventsNewSessions()
    {
        // 1. Create door with daily limit of 2
        var createDto = new CreateDoorDto
        {
            Name = "Limited Door",
            Description = "A door with daily session limit",
            Category = "Test",
            ExecutablePath = CreateTestExecutable(),
            DailyLimit = 2
        };

        var door = await _doorService.CreateDoorAsync(createDto, 1);

        // 2. Start and end first session
        var session1 = await _doorService.StartDoorSessionAsync(door.Id, 1);
        await _doorService.EndDoorSessionAsync(session1.SessionId, 0);

        // 3. Start and end second session
        var session2 = await _doorService.StartDoorSessionAsync(door.Id, 1);
        await _doorService.EndDoorSessionAsync(session2.SessionId, 0);

        // 4. Check limit reached
        var limitReached = await _doorService.HasUserReachedDailyLimitAsync(1, door.Id);
        limitReached.Should().BeTrue();

        // 5. Attempt third session should fail
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _doorService.StartDoorSessionAsync(door.Id, 1));
    }

    #endregion

    #region Door Configuration Integration Tests

    [Fact]
    public async Task DoorConfiguration_SetAndRetrieve_WorksCorrectly()
    {
        // 1. Create door
        var createDto = new CreateDoorDto
        {
            Name = "Configurable Door",
            Category = "Test",
            ExecutablePath = CreateTestExecutable()
        };

        var door = await _doorService.CreateDoorAsync(createDto, 1);

        // 2. Set various configuration values
        await _doorService.SetDoorConfigAsync(door.Id, "timeout", "300", "int");
        await _doorService.SetDoorConfigAsync(door.Id, "debug_mode", "true", "bool");
        await _doorService.SetDoorConfigAsync(door.Id, "welcome_message", "Welcome to the game!", "string");

        // 3. Retrieve configurations
        var configs = await _doorService.GetDoorConfigsAsync(door.Id);
        configs.Should().HaveCount(3);

        var timeoutConfig = configs.First(c => c.ConfigKey == "timeout");
        timeoutConfig.ConfigValue.Should().Be("300");
        timeoutConfig.ConfigType.Should().Be("int");

        // 4. Get specific config value
        var debugValue = await _doorService.GetDoorConfigValueAsync(door.Id, "debug_mode");
        debugValue.Should().Be("true");

        // 5. Update existing config
        await _doorService.SetDoorConfigAsync(door.Id, "timeout", "600", "int");
        var updatedValue = await _doorService.GetDoorConfigValueAsync(door.Id, "timeout");
        updatedValue.Should().Be("600");

        // 6. Delete config
        await _doorService.DeleteDoorConfigAsync(door.Id, "debug_mode");
        var configsAfterDelete = await _doorService.GetDoorConfigsAsync(door.Id);
        configsAfterDelete.Should().HaveCount(2);
    }

    #endregion

    #region Statistics Integration Tests

    [Fact]
    public async Task DoorStatistics_MultipleSessionsAndUsers_CalculatesCorrectly()
    {
        // 1. Create door
        var createDto = new CreateDoorDto
        {
            Name = "Statistics Test Door",
            Category = "Test",
            ExecutablePath = CreateTestExecutable()
        };

        var door = await _doorService.CreateDoorAsync(createDto, 1);

        // 2. Add second test user
        await _databaseManager.ExecuteAsync(
            "INSERT INTO Users (Handle, RealName, SecurityLevel) VALUES ('User2', 'Second User', 50)");

        // 3. Simulate multiple sessions for user 1
        for (int i = 0; i < 3; i++)
        {
            var session = await _doorService.StartDoorSessionAsync(door.Id, 1);
            await Task.Delay(100); // Simulate session time
            await _doorService.EndDoorSessionAsync(session.SessionId, 0);
            await _doorService.UpdateDoorStatisticsAsync(door.Id, 1, 120, 100 + i * 50); // Different scores
        }

        // 4. Simulate sessions for user 2
        for (int i = 0; i < 2; i++)
        {
            var session = await _doorService.StartDoorSessionAsync(door.Id, 2);
            await Task.Delay(50);
            await _doorService.EndDoorSessionAsync(session.SessionId, 0);
            await _doorService.UpdateDoorStatisticsAsync(door.Id, 2, 90, 200 + i * 25);
        }

        // 5. Check user 1 statistics
        var user1Stats = await _doorService.GetUserDoorStatisticsAsync(door.Id, 1);
        user1Stats.Should().NotBeNull();
        user1Stats!.TotalSessions.Should().Be(3);
        user1Stats.TotalTime.Should().Be(360); // 3 * 120 seconds
        user1Stats.HighScore.Should().Be(200); // Highest score from sessions

        // 6. Check user 2 statistics
        var user2Stats = await _doorService.GetUserDoorStatisticsAsync(door.Id, 2);
        user2Stats.Should().NotBeNull();
        user2Stats!.TotalSessions.Should().Be(2);
        user2Stats.HighScore.Should().Be(225);

        // 7. Check overall door statistics
        var doorStats = await _doorService.GetDoorStatisticsAsync(door.Id);
        doorStats.Should().HaveCount(2); // Two users played

        // 8. Check system statistics
        var systemStats = await _doorService.GetDoorSystemStatisticsAsync();
        systemStats.Should().NotBeNull();
        systemStats.TotalDoors.Should().BeGreaterThanOrEqualTo(1);
        systemStats.TotalSessions.Should().BeGreaterThanOrEqualTo(5);
    }

    #endregion

    #region Maintenance Integration Tests

    [Fact]
    public async Task MaintenanceOperations_CleanupAndValidation_WorkCorrectly()
    {
        // 1. Create door with issues
        var createDto = new CreateDoorDto
        {
            Name = "Maintenance Test Door",
            Category = "Test",
            ExecutablePath = "/nonexistent/path/door.exe", // Invalid path
            WorkingDirectory = "/nonexistent/directory",
            RequiresDosBox = true,
            DosBoxConfigPath = "/nonexistent/dosbox.conf"
        };

        var door = await _doorService.CreateDoorAsync(createDto, 1);

        // 2. Validate door configuration
        var issues = await _doorService.ValidateDoorConfigurationAsync(door.Id);
        issues.Should().NotBeEmpty();
        issues.Should().Contain("Executable file not found");
        issues.Should().Contain("Working directory not found");
        issues.Should().Contain("DOSBox not available but required");
        issues.Should().Contain("DOSBox config file not found");

        // 3. Create expired sessions for cleanup
        var oldSessionId = Guid.NewGuid().ToString();
        await _databaseManager.ExecuteAsync(@"
            INSERT INTO DoorSessions (SessionId, DoorId, UserId, StartTime, Status)
            VALUES (@SessionId, @DoorId, 1, datetime('now', '-2 hours'), 'running')",
            new { SessionId = oldSessionId, DoorId = door.Id });

        // 4. Cleanup expired sessions
        var cleanedSessions = await _doorService.CleanupExpiredSessionsAsync();
        cleanedSessions.Should().BeGreaterThan(0);

        // 5. Create orphaned drop files
        var orphanedFile = Path.Combine(_testDropFilePath, $"{Guid.NewGuid()}_door_sys");
        await File.WriteAllTextAsync(orphanedFile, "orphaned content");

        // 6. Cleanup orphaned files
        var cleanedFiles = await _doorService.CleanupOrphanedFilesAsync();
        cleanedFiles.Should().BeGreaterThan(0);
        File.Exists(orphanedFile).Should().BeFalse();
    }

    #endregion

    #region Logging Integration Tests

    [Fact]
    public async Task DoorLogging_VariousEvents_LogsCorrectly()
    {
        // 1. Create door
        var createDto = new CreateDoorDto
        {
            Name = "Logging Test Door",
            Category = "Test",
            ExecutablePath = CreateTestExecutable()
        };

        var door = await _doorService.CreateDoorAsync(createDto, 1);

        // 2. Log various events
        await _doorService.LogDoorEventAsync(door.Id, "info", "Door started");
        await _doorService.LogDoorEventAsync(door.Id, "warning", "High memory usage");
        await _doorService.LogDoorEventAsync(door.Id, "error", "Connection lost", "Detailed error information");

        // 3. Start session to generate session logs
        var session = await _doorService.StartDoorSessionAsync(door.Id, 1);
        await _doorService.EndDoorSessionAsync(session.SessionId, 0);

        // 4. Retrieve all logs for the door
        var logs = await _doorService.GetDoorLogsAsync(doorId: door.Id);
        logs.Should().HaveCountGreaterThanOrEqualTo(3); // At least our manual logs plus session logs

        // 5. Filter logs by level
        var errorLogs = await _doorService.GetDoorLogsAsync(doorId: door.Id, level: "error");
        errorLogs.Should().HaveCountGreaterThanOrEqualTo(1);

        var errorLog = errorLogs.First(l => l.Message == "Connection lost");
        errorLog.LogLevel.Should().Be("error");
        errorLog.Details.Should().Be("Detailed error information");
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public async Task ErrorHandling_InvalidOperations_HandleGracefully()
    {
        // 1. Try to get non-existent door
        var nonExistentDoor = await _doorService.GetDoorAsync(999);
        nonExistentDoor.Should().BeNull();

        // 2. Try to delete non-existent door
        var deleteResult = await _doorService.DeleteDoorAsync(999);
        deleteResult.Should().BeFalse();

        // 3. Try to access door with non-existent user
        var canAccess = await _doorService.CanUserAccessDoorAsync(999, 1);
        canAccess.Should().BeFalse();

        // 4. Try to start session for non-existent door
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _doorService.StartDoorSessionAsync(999, 1));

        // 5. Try to end non-existent session
        var endResult = await _doorService.EndDoorSessionAsync("non-existent-session", 0);
        endResult.Should().BeFalse();

        // 6. Try to generate drop file for non-existent door
        await Assert.ThrowsAsync<ArgumentException>(
            () => _doorService.GenerateDropFileAsync(999, 1, "test-session"));
    }

    #endregion

    #region Helper Methods

    private string CreateTestExecutable()
    {
        var exePath = Path.Combine(_testBasePath, "test_door.exe");
        File.WriteAllText(exePath, "Mock executable content");
        return exePath;
    }

    #endregion
}
