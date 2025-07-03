using System.Diagnostics;
using Blackboard.Core.DTOs;
using Blackboard.Core.Models;
using Blackboard.Core.Services;
using Blackboard.Data;
using FluentAssertions;
using Serilog;
using Moq;
using Xunit;

namespace Blackboard.Core.Tests.Services;

public class DoorServiceTests : IDisposable
{
    private readonly Mock<IDatabaseManager> _mockDatabaseManager;
    private readonly Mock<ILogger> _mockLogger;
    private readonly DoorService _doorService;
    private readonly string _testBasePath;
    private readonly string _testDropFilePath;

    public DoorServiceTests()
    {
        _mockDatabaseManager = new Mock<IDatabaseManager>();
        _mockLogger = new Mock<ILogger>();
        
        _testBasePath = Path.Combine(Path.GetTempPath(), "blackboard_test_doors");
        _testDropFilePath = Path.Combine(Path.GetTempPath(), "blackboard_test_dropfiles");
        
        _doorService = new DoorService(
            _mockDatabaseManager.Object,
            _mockLogger.Object,
            _testBasePath,
            _testDropFilePath
        );

        // Ensure test directories exist
        Directory.CreateDirectory(_testBasePath);
        Directory.CreateDirectory(_testDropFilePath);
    }

    public void Dispose()
    {
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

    #region Door Management Tests

    [Fact]
    public async Task GetAllDoorsAsync_ReturnsAllDoors()
    {
        // Arrange
        var expectedDoors = new List<dynamic>
        {
            CreateDoorData(1, "Door1", "Test Door 1", activeSessions: 0, totalSessions: 5, lastPlayed: DateTime.UtcNow),
            CreateDoorData(2, "Door2", "Test Door 2", activeSessions: 1, totalSessions: 3, lastPlayed: DateTime.UtcNow)
        };

        _mockDatabaseManager
            .Setup(x => x.QueryAsync<dynamic>(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(expectedDoors);

        // Act
        var result = await _doorService.GetAllDoorsAsync();

        // Assert
        result.Should().HaveCount(2);
        result.First().Name.Should().Be("Door1");
        _mockDatabaseManager.Verify(x => x.QueryAsync<dynamic>(It.IsAny<string>(), It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task GetDoorAsync_WithValidId_ReturnsDoor()
    {
        // Arrange
        var doorId = 1;
        var doorData = CreateDoorData(doorId, "TestDoor", "Test");

        _mockDatabaseManager
            .Setup(x => x.QueryFirstOrDefaultAsync<dynamic>(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.FromResult<dynamic>(doorData));

        // Act
        var result = await _doorService.GetDoorAsync(doorId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(doorId);
        result.Name.Should().Be("TestDoor");
    }

    [Fact]
    public async Task GetDoorAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var doorId = 999;

        _mockDatabaseManager
            .Setup(x => x.QueryFirstOrDefaultAsync<dynamic>(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.FromResult<dynamic?>(null));

        // Act
        var result = await _doorService.GetDoorAsync(doorId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateDoorAsync_ValidInput_CreatesDoor()
    {
        // Arrange
        var createDto = new CreateDoorDto
        {
            Name = "New Door",
            Description = "A new test door",
            Category = "Test",
            ExecutablePath = "/path/to/door.exe",
            DropFileType = "DOOR.SYS",
            MinimumLevel = 10,
            TimeLimit = 60,
            DailyLimit = 3
        };

        var newDoorId = 5;
        _mockDatabaseManager
            .Setup(x => x.QueryFirstAsync<int>(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(newDoorId);

        var createdDoor = CreateDoorData(newDoorId, createDto.Name, createDto.Description, activeSessions: 0, totalSessions: 0, lastPlayed: null);
        _mockDatabaseManager
            .Setup(x => x.QueryFirstOrDefaultAsync<dynamic>(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.FromResult<dynamic>(createdDoor));

        // Act
        var result = await _doorService.CreateDoorAsync(createDto, 1);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be(createDto.Name);
        result.Description.Should().Be(createDto.Description);
        _mockDatabaseManager.Verify(x => x.QueryFirstAsync<int>(It.IsAny<string>(), It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task DeleteDoorAsync_WithActiveSessions_ThrowsException()
    {
        // Arrange
        var doorId = 1;
        var door = new DoorDto { Id = doorId, Name = "TestDoor", IsActive = true };
        var activeSessions = new List<DoorSessionDto> { new() { Id = 1, DoorId = doorId, Status = "running" } };

        _mockDatabaseManager
            .Setup(x => x.QueryFirstOrDefaultAsync<dynamic>(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.FromResult<dynamic>(CreateDoorData(doorId, "TestDoor", "Test", activeSessions: 1, totalSessions: 5, lastPlayed: DateTime.UtcNow)));

        _mockDatabaseManager
            .Setup(x => x.QueryAsync<dynamic>(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(new List<dynamic> { CreateSessionData(1, "test-session", "running") });

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _doorService.DeleteDoorAsync(doorId));
    }

    #endregion

    #region Access Control Tests

    [Fact]
    public async Task CanUserAccessDoorAsync_ValidUser_ReturnsTrue()
    {
        // Arrange
        var userId = 1;
        var doorId = 1;

        // Create a test executable file
        var testExePath = Path.Combine(_testBasePath, "test.exe");
        await File.WriteAllTextAsync(testExePath, "test");

        // Mock door data - this will be called by GetDoorAsync multiple times
        var doorData = CreateDoorData(doorId, "TestDoor", "Test", 
            activeSessions: 0, totalSessions: 0, lastPlayed: null, 
            minimumLevel: 10, maximumLevel: 100, executablePath: testExePath, 
            isActive: true);
        
        _mockDatabaseManager
            .Setup(x => x.QueryFirstOrDefaultAsync<dynamic>(It.Is<string>(s => s.Contains("FROM Doors d") && s.Contains("WHERE d.Id = @DoorId")), It.IsAny<object>()))
            .Returns(Task.FromResult<dynamic>(doorData));

        // Mock user level (50 is between min 10 and max 100)
        _mockDatabaseManager
            .Setup(x => x.QueryFirstOrDefaultAsync<int?>(It.Is<string>(s => s.Contains("SecurityLevel")), It.IsAny<object>()))
            .ReturnsAsync(50);

        // Mock no deny permissions
        _mockDatabaseManager
            .Setup(x => x.QueryAsync<string>(It.Is<string>(s => s.Contains("AccessType")), It.IsAny<object>()))
            .ReturnsAsync(new List<string>());

        // Mock daily limit not reached
        _mockDatabaseManager
            .Setup(x => x.QueryFirstAsync<int>(It.Is<string>(s => s.Contains("COUNT(*)")), It.IsAny<object>()))
            .ReturnsAsync(0);

        // Act
        var result = await _doorService.CanUserAccessDoorAsync(userId, doorId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanUserAccessDoorAsync_InsufficientLevel_ReturnsFalse()
    {
        // Arrange
        var userId = 1;
        var doorId = 1;

        // Mock door data with high minimum level
        var doorData = CreateDoorData(doorId, "TestDoor", "Test Door", isActive: true, minimumLevel: 100, maximumLevel: 255);
        _mockDatabaseManager
            .Setup(x => x.QueryFirstOrDefaultAsync<dynamic>(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.FromResult<dynamic>(doorData));

        // Mock user level too low
        _mockDatabaseManager
            .Setup(x => x.QueryFirstOrDefaultAsync<int?>(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(50);

        // Act
        var result = await _doorService.CanUserAccessDoorAsync(userId, doorId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasUserReachedDailyLimitAsync_LimitReached_ReturnsTrue()
    {
        // Arrange
        var userId = 1;
        var doorId = 1;
        var dailyLimit = 3;

        // Mock door with daily limit
        var doorData = CreateDoorData(doorId, "TestDoor", "Test Door", dailyLimit: dailyLimit);
        _mockDatabaseManager
            .Setup(x => x.QueryFirstOrDefaultAsync<dynamic>(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.FromResult<dynamic>(doorData));

        // Mock user has reached daily limit
        _mockDatabaseManager
            .Setup(x => x.QueryFirstAsync<int>(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(dailyLimit);

        // Act
        var result = await _doorService.HasUserReachedDailyLimitAsync(userId, doorId);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Door Session Tests

    [Fact]
    public async Task StartDoorSessionAsync_ValidInput_ReturnsSession()
    {
        // Arrange
        var userId = 1;
        var doorId = 1;
        var sessionDbId = 100;

        // Create a test executable file
        var testExePath = Path.Combine(_testBasePath, "test.exe");
        await File.WriteAllTextAsync(testExePath, "test");

        // Mock door data for GetDoorAsync calls
        var doorData = CreateDoorData(doorId, "TestDoor", "Test Door", 
            isActive: true, minimumLevel: 10, maximumLevel: 100, executablePath: testExePath);
        
        _mockDatabaseManager
            .Setup(x => x.QueryFirstOrDefaultAsync<dynamic>(It.Is<string>(s => s.Contains("SELECT d.*") && s.Contains("FROM Doors d") && s.Contains("WHERE d.Id = @DoorId")), It.IsAny<object>()))
            .Returns(Task.FromResult<dynamic?>(doorData));

        // Mock user level for CanUserAccessDoorAsync
        _mockDatabaseManager
            .Setup(x => x.QueryFirstOrDefaultAsync<int?>(It.Is<string>(s => s.Contains("SecurityLevel")), It.IsAny<object>()))
            .ReturnsAsync(50);

        // Mock no deny permissions for CanUserAccessDoorAsync
        _mockDatabaseManager
            .Setup(x => x.QueryAsync<string>(It.Is<string>(s => s.Contains("AccessType")), It.IsAny<object>()))
            .ReturnsAsync(new List<string>());

        // Mock daily limit not reached for CanUserAccessDoorAsync
        _mockDatabaseManager
            .Setup(x => x.QueryFirstAsync<int>(It.Is<string>(s => s.Contains("COUNT(*)")), It.IsAny<object>()))
            .ReturnsAsync(0);

        // Mock session creation
        _mockDatabaseManager
            .Setup(x => x.QueryFirstAsync<int>(It.Is<string>(s => s.Contains("INSERT INTO DoorSessions")), It.IsAny<object>()))
            .ReturnsAsync(sessionDbId);

        // Mock session retrieval
        var sessionData = CreateSessionData(sessionDbId, "test-session", "starting", doorId, userId);
        dynamic extendedSessionData = sessionData;
        extendedSessionData.DoorName = "TestDoor";
        extendedSessionData.UserHandle = "TestUser";
        extendedSessionData.StartTime = DateTime.UtcNow;
        extendedSessionData.EndTime = (DateTime?)null;
        extendedSessionData.LastActivity = DateTime.UtcNow;
        
        _mockDatabaseManager
            .Setup(x => x.QueryFirstOrDefaultAsync<dynamic>(It.Is<string>(s => s.Contains("FROM DoorSessions ds")), It.IsAny<object>()))
            .Returns(Task.FromResult<dynamic?>(extendedSessionData));

        // Act
        var result = await _doorService.StartDoorSessionAsync(doorId, userId);

        // Assert
        result.Should().NotBeNull();
        result.DoorId.Should().Be(doorId);
        result.UserId.Should().Be(userId);
        result.Status.Should().Be("starting");
    }

    [Fact]
    public async Task EndDoorSessionAsync_ValidSession_ReturnsTrue()
    {
        // Arrange
        var sessionId = "test-session";
        var exitCode = 0;

        // Set up the sequence of database calls:
        // 1. ExecuteAsync for updating session status - called first
        _mockDatabaseManager
            .SetupSequence(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(1)  // First call for session update
            .ReturnsAsync(1); // Second call for statistics if needed

        // 2. QueryFirstOrDefaultAsync<string> for CleanupDropFileAsync (return null - no file to clean)
        _mockDatabaseManager
            .Setup(x => x.QueryFirstOrDefaultAsync<string>(It.Is<string>(s => s.Contains("DropFilePath")), It.IsAny<object>()))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _doorService.EndDoorSessionAsync(sessionId, exitCode);

        // Assert
        result.Should().BeTrue();
        _mockDatabaseManager.Verify(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()), Times.AtLeastOnce);
    }

    #endregion

    #region Drop File Tests

    [Fact]
    public async Task GenerateDropFileAsync_ValidInput_GeneratesFile()
    {
        // Arrange
        var doorId = 1;
        var userId = 1;
        var sessionId = "test-session";

        // Mock door data
        var doorData = CreateDoorData(doorId, "TestDoor", "Test Door", dropFileType: "DOOR.SYS", serialPort: "COM1");
        _mockDatabaseManager
            .Setup(x => x.QueryFirstOrDefaultAsync<dynamic>(It.Is<string>(s => s.Contains("FROM Doors d") && s.Contains("WHERE d.Id = @DoorId")), It.IsAny<object>()))
            .Returns(Task.FromResult<dynamic>(doorData));

        // Mock user data
        var userData = CreateUserData("TestUser", "Test User", "Test City", 50, 60);
        _mockDatabaseManager
            .Setup(x => x.QueryFirstOrDefaultAsync<dynamic>(It.Is<string>(s => s.Contains("FROM Users WHERE Id")), It.IsAny<object>()))
            .Returns(Task.FromResult<dynamic>(userData));

        // Mock session update
        _mockDatabaseManager
            .Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(1);

        // Act
        var result = await _doorService.GenerateDropFileAsync(doorId, userId, sessionId);

        // Assert
        result.Should().NotBeNull();
        result.FileName.Should().NotBeNullOrEmpty();
        result.FilePath.Should().NotBeNullOrEmpty();
        File.Exists(result.FilePath).Should().BeTrue();
    }

    [Fact]
    public async Task CleanupDropFileAsync_ValidSession_DeletesFile()
    {
        // Arrange
        var sessionId = "test-session";
        var filePath = Path.Combine(_testDropFilePath, $"{sessionId}_door_sys");
        
        // Create test file
        await File.WriteAllTextAsync(filePath, "test content");
        File.Exists(filePath).Should().BeTrue();

        // Mock database to return the file path
        _mockDatabaseManager
            .Setup(x => x.QueryFirstOrDefaultAsync<string>(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(filePath);

        // Act
        var result = await _doorService.CleanupDropFileAsync(sessionId);

        // Assert
        result.Should().BeTrue();
        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public void GetDropFileTemplate_DoorSys_ReturnsTemplate()
    {
        // Act
        var result = _doorService.GetDropFileTemplate("DOOR.SYS");

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("{USER_HANDLE}");
        result.Should().Contain("{COM_PORT}");
        result.Should().Contain("{BAUD_RATE}");
    }

    [Fact]
    public async Task ValidateDropFileAsync_ValidFile_ReturnsTrue()
    {
        // Arrange
        var filePath = Path.Combine(_testDropFilePath, "test_door.sys");
        var content = @"COM1:
38400
8
1
test-session
TestUser
Test User
Test City
50
60";
        await File.WriteAllTextAsync(filePath, content);

        // Act
        var result = await _doorService.ValidateDropFileAsync(filePath);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region DOSBox Tests

    [Fact]
    public async Task GenerateDosBoxConfigAsync_ValidInput_GeneratesConfig()
    {
        // Arrange
        var doorId = 1;
        var sessionId = "test-session";

        var doorData = CreateDoorData(doorId, "TestDoor", "Test Door", 
            workingDirectory: "/test/path");
        dynamic extendedDoorData = doorData;
        extendedDoorData.MemorySize = 16;
        
        _mockDatabaseManager
            .Setup(x => x.QueryFirstOrDefaultAsync<dynamic>(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.FromResult<dynamic>(extendedDoorData));

        // Act
        var result = await _doorService.GenerateDosBoxConfigAsync(doorId, sessionId);

        // Assert
        result.Should().NotBeNullOrEmpty();
        File.Exists(result).Should().BeTrue();
        
        var configContent = await File.ReadAllTextAsync(result);
        configContent.Should().Contain("[sdl]");
        configContent.Should().Contain("[dosbox]");
        configContent.Should().Contain("[serial]");
        configContent.Should().Contain("[autoexec]");
    }

    [Fact]
    public void IsDosBoxAvailable_WhenNotInstalled_ReturnsFalse()
    {
        // Act
        var result = _doorService.IsDosBoxAvailable();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Statistics Tests

    [Fact]
    public async Task GetDoorSystemStatisticsAsync_ReturnsStatistics()
    {
        // Arrange
        var statsData = new 
        { 
            TotalDoors = 5,
            ActiveDoors = 3,
            TotalSessions = 100,
            ActiveSessions = 2,
            TotalSessionTime = 12000,
            UniquePlayersToday = 15
        };

        _mockDatabaseManager
            .Setup(x => x.QueryFirstAsync<DoorSystemStatisticsDto>(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(new DoorSystemStatisticsDto
            {
                TotalDoors = statsData.TotalDoors,
                ActiveDoors = statsData.ActiveDoors,
                TotalSessions = statsData.TotalSessions,
                ActiveSessions = statsData.ActiveSessions,
                TotalSessionTime = statsData.TotalSessionTime,
                UniquePlayersToday = statsData.UniquePlayersToday
            });

        // Mock most played doors
        _mockDatabaseManager
            .Setup(x => x.QueryAsync<dynamic>(It.Is<string>(s => s.Contains("ORDER BY COUNT(ds.Id) DESC")), It.IsAny<object>()))
            .ReturnsAsync(new List<dynamic>());

        // Mock recent sessions
        _mockDatabaseManager
            .Setup(x => x.QueryAsync<dynamic>(It.Is<string>(s => s.Contains("ORDER BY ds.StartTime DESC")), It.IsAny<object>()))
            .ReturnsAsync(new List<dynamic>());

        // Mock categories
        _mockDatabaseManager
            .Setup(x => x.QueryAsync<string>(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(new List<string> { "Action", "Strategy" });

        // Act
        var result = await _doorService.GetDoorSystemStatisticsAsync();

        // Assert
        result.Should().NotBeNull();
        result.TotalDoors.Should().Be(5);
        result.ActiveDoors.Should().Be(3);
        result.TotalSessions.Should().Be(100);
    }

    [Fact]
    public async Task UpdateDoorStatisticsAsync_ValidInput_UpdatesStats()
    {
        // Arrange
        var doorId = 1;
        var userId = 1;
        var sessionTime = 300;
        var score = 1000;

        _mockDatabaseManager
            .Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(1);

        // Act
        var result = await _doorService.UpdateDoorStatisticsAsync(doorId, userId, sessionTime, score);

        // Assert
        result.Should().BeTrue();
        _mockDatabaseManager.Verify(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()), Times.Once);
    }

    #endregion

    #region Permissions Tests

    [Fact]
    public async Task AddDoorPermissionAsync_ValidInput_AddsPermission()
    {
        // Arrange
        var doorId = 1;
        var userId = 1;
        var accessType = "allow";
        var grantedBy = 2;

        var permissionData = new 
        { 
            Id = 1, 
            DoorId = doorId, 
            UserId = userId, 
            AccessType = accessType, 
            GrantedBy = grantedBy, 
            GrantedAt = DateTime.UtcNow,
            ExpiresAt = (DateTime?)null
        };

        _mockDatabaseManager
            .Setup(x => x.QueryFirstAsync<DoorPermissionDto>(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(new DoorPermissionDto
            {
                Id = permissionData.Id,
                DoorId = permissionData.DoorId,
                UserId = permissionData.UserId,
                AccessType = permissionData.AccessType,
                GrantedBy = permissionData.GrantedBy,
                GrantedAt = permissionData.GrantedAt,
                ExpiresAt = permissionData.ExpiresAt
            });

        // Act
        var result = await _doorService.AddDoorPermissionAsync(doorId, userId, null, accessType, grantedBy);

        // Assert
        result.Should().NotBeNull();
        result.DoorId.Should().Be(doorId);
        result.UserId.Should().Be(userId);
        result.AccessType.Should().Be(accessType);
    }

    #endregion

    #region Maintenance Tests

    [Fact]
    public async Task ValidateDoorConfigurationAsync_MissingExecutable_ReturnsIssue()
    {
        // Arrange
        var doorId = 1;
        var doorData = CreateDoorData(doorId, "TestDoor", "Test Door", 
            executablePath: "/nonexistent/path/door.exe", 
            workingDirectory: "/test/path", 
            requiresDosBox: false);

        _mockDatabaseManager
            .Setup(x => x.QueryFirstOrDefaultAsync<dynamic>(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.FromResult<dynamic>(doorData));

        // Act
        var result = await _doorService.ValidateDoorConfigurationAsync(doorId);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain("Executable file not found");
    }

    [Fact]
    public async Task CleanupExpiredSessionsAsync_RemovesExpiredSessions()
    {
        // Arrange
        _mockDatabaseManager
            .Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(3);

        // Act
        var result = await _doorService.CleanupExpiredSessionsAsync();

        // Assert
        result.Should().Be(3);
        _mockDatabaseManager.Verify(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()), Times.Once);
    }

    #endregion

    #region Logging Tests

    [Fact]
    public async Task LogDoorEventAsync_ValidInput_LogsEvent()
    {
        // Arrange
        var doorId = 1;
        var level = "info";
        var message = "Test log message";

        _mockDatabaseManager
            .Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(1);

        // Act
        await _doorService.LogDoorEventAsync(doorId, level, message);

        // Assert
        _mockDatabaseManager.Verify(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task GetDoorLogsAsync_ReturnsLogs()
    {
        // Arrange
        var logs = new List<DoorLog>
        {
            new() { Id = 1, DoorId = 1, LogLevel = "info", Message = "Test message 1", Timestamp = DateTime.UtcNow },
            new() { Id = 2, DoorId = 1, LogLevel = "warning", Message = "Test message 2", Timestamp = DateTime.UtcNow }
        };

        _mockDatabaseManager
            .Setup(x => x.QueryAsync<DoorLog>(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(logs);

        // Act
        var result = await _doorService.GetDoorLogsAsync(doorId: 1);

        // Assert
        result.Should().HaveCount(2);
        result.First().Message.Should().Be("Test message 1");
    }

    #endregion

    #region Helper Methods

    private void SetupValidAccessMocks(int userId, int doorId)
    {
        // Mock door data
        var doorData = CreateDoorData(doorId, "TestDoor", "Test Door", 
            isActive: true, minimumLevel: 10, maximumLevel: 100, executablePath: "/test/door.exe");
        _mockDatabaseManager
            .Setup(x => x.QueryFirstOrDefaultAsync<dynamic>(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.FromResult<dynamic>(doorData));

        // Mock user level
        _mockDatabaseManager
            .Setup(x => x.QueryFirstOrDefaultAsync<int?>(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(50);

        // Mock no deny permissions
        _mockDatabaseManager
            .Setup(x => x.QueryAsync<string>(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(new List<string>());

        // Mock daily limit not reached
        _mockDatabaseManager
            .Setup(x => x.QueryFirstAsync<int>(It.IsAny<string>(), It.IsAny<object>()))
            .ReturnsAsync(0);
    }

    private dynamic CreateDoorData(int id, string name = "TestDoor", string description = "Test", 
        int activeSessions = 0, int totalSessions = 0, DateTime? lastPlayed = null, 
        string category = "Games", string executablePath = "/path/to/door.exe",
        bool isActive = true, int minimumLevel = 0, int maximumLevel = 999,
        int timeLimit = 60, int dailyLimit = 5, string dropFileType = "DOOR.SYS",
        string? serialPort = null, string workingDirectory = "", bool requiresDosBox = false)
    {
        dynamic doorData = new System.Dynamic.ExpandoObject();
        doorData.Id = id;
        doorData.Name = name;
        doorData.Description = description;
        doorData.Category = category;
        doorData.ExecutablePath = executablePath;
        doorData.CommandLine = "";
        doorData.WorkingDirectory = workingDirectory;
        doorData.DropFileType = dropFileType;
        doorData.DropFileLocation = "";
        doorData.IsActive = isActive;
        doorData.RequiresDosBox = requiresDosBox;
        doorData.DosBoxConfigPath = (string?)null;
        doorData.SerialPort = serialPort;
        doorData.MemorySize = 640;
        doorData.MinimumLevel = minimumLevel;
        doorData.MaximumLevel = maximumLevel;
        doorData.TimeLimit = timeLimit;
        doorData.DailyLimit = dailyLimit;
        doorData.Cost = 0;
        doorData.SchedulingEnabled = false;
        doorData.AvailableHours = "";
        doorData.TimeZone = "UTC";
        doorData.MultiNodeEnabled = false;
        doorData.MaxPlayers = 1;
        doorData.InterBbsEnabled = false;
        doorData.CreatedAt = DateTime.UtcNow;
        doorData.UpdatedAt = DateTime.UtcNow;
        doorData.CreatedBy = 1;
        doorData.ActiveSessions = activeSessions;
        doorData.TotalSessions = totalSessions;
        doorData.LastPlayed = lastPlayed;
        return doorData;
    }

    private dynamic CreateSessionData(int id, string sessionId, string status = "running", 
        int doorId = 1, int userId = 1)
    {
        dynamic sessionData = new System.Dynamic.ExpandoObject();
        sessionData.Id = id;
        sessionData.SessionId = sessionId;
        sessionData.Status = status;
        sessionData.DoorId = doorId;
        sessionData.UserId = userId;
        sessionData.StartTime = DateTime.UtcNow;
        sessionData.EndTime = (DateTime?)null;
        sessionData.ExitCode = (int?)null;
        sessionData.ErrorMessage = (string?)null;
        sessionData.NodeNumber = (int?)null;
        sessionData.DoorName = "TestDoor";
        sessionData.UserHandle = "TestUser";
        sessionData.Duration = 0;
        sessionData.LastActivity = DateTime.UtcNow;
        return sessionData;
    }

    private dynamic CreateUserData(string handle = "TestUser", string realName = "Test User", 
        string location = "Test City", int securityLevel = 50, int timeLeft = 60)
    {
        dynamic userData = new System.Dynamic.ExpandoObject();
        userData.Handle = handle;
        userData.RealName = realName;
        userData.Location = location;
        userData.SecurityLevel = securityLevel;
        userData.TimeLeft = timeLeft;
        userData.LastLoginAt = DateTime.UtcNow;
        return userData;
    }
    
    #endregion
}
