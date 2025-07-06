using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Blackboard.Core.DTOs;
using Blackboard.Core.Models;
using Blackboard.Data;
using Serilog;

namespace Blackboard.Core.Services;

public class DoorService : IDoorService
{
    private readonly IDatabaseManager _databaseManager;
    private readonly string _doorBasePath;
    private readonly string _dosBoxPath;
    private readonly string _dropFileBasePath;
    private readonly ILogger _logger;

    public DoorService(IDatabaseManager databaseManager, ILogger logger,
        string doorBasePath = "doors", string dropFileBasePath = "temp/dropfiles",
        string dosBoxPath = "dosbox")
    {
        _databaseManager = databaseManager;
        _logger = logger;
        _doorBasePath = doorBasePath;
        _dropFileBasePath = dropFileBasePath;
        _dosBoxPath = dosBoxPath;

        // Ensure directories exist
        Directory.CreateDirectory(_doorBasePath);
        Directory.CreateDirectory(_dropFileBasePath);
    }

    #region Door Management

    public async Task<IEnumerable<DoorDto>> GetAllDoorsAsync()
    {
        const string sql = @"
            SELECT d.*, 
                   (SELECT COUNT(*) FROM DoorSessions ds WHERE ds.DoorId = d.Id AND ds.Status = 'running') as ActiveSessions,
                   (SELECT COUNT(*) FROM DoorSessions ds WHERE ds.DoorId = d.Id) as TotalSessions,
                   (SELECT MAX(ds.StartTime) FROM DoorSessions ds WHERE ds.DoorId = d.Id) as LastPlayed
            FROM Doors d
            ORDER BY d.Name";

        var doors = await _databaseManager.QueryAsync<dynamic>(sql);
        return doors.Select(MapToDoorDto);
    }

    public async Task<IEnumerable<DoorDto>> GetActiveDoorsAsync()
    {
        const string sql = @"
            SELECT d.*, 
                   (SELECT COUNT(*) FROM DoorSessions ds WHERE ds.DoorId = d.Id AND ds.Status = 'running') as ActiveSessions,
                   (SELECT COUNT(*) FROM DoorSessions ds WHERE ds.DoorId = d.Id) as TotalSessions,
                   (SELECT MAX(ds.StartTime) FROM DoorSessions ds WHERE ds.DoorId = d.Id) as LastPlayed
            FROM Doors d
            WHERE d.IsActive = 1
            ORDER BY d.Name";

        var doors = await _databaseManager.QueryAsync<dynamic>(sql);
        return doors.Select(MapToDoorDto);
    }

    public async Task<DoorDto?> GetDoorAsync(int doorId)
    {
        const string sql = @"
            SELECT d.*, 
                   (SELECT COUNT(*) FROM DoorSessions ds WHERE ds.DoorId = d.Id AND ds.Status = 'running') as ActiveSessions,
                   (SELECT COUNT(*) FROM DoorSessions ds WHERE ds.DoorId = d.Id) as TotalSessions,
                   (SELECT MAX(ds.StartTime) FROM DoorSessions ds WHERE ds.DoorId = d.Id) as LastPlayed
            FROM Doors d
            WHERE d.Id = @DoorId";

        var door = await _databaseManager.QueryFirstOrDefaultAsync<dynamic>(sql, new { DoorId = doorId });
        return door != null ? MapToDoorDto(door) : null;
    }

    public async Task<DoorDto?> GetDoorByNameAsync(string name)
    {
        const string sql = @"
            SELECT d.*, 
                   (SELECT COUNT(*) FROM DoorSessions ds WHERE ds.DoorId = d.Id AND ds.Status = 'running') as ActiveSessions,
                   (SELECT COUNT(*) FROM DoorSessions ds WHERE ds.DoorId = d.Id) as TotalSessions,
                   (SELECT MAX(ds.StartTime) FROM DoorSessions ds WHERE ds.DoorId = d.Id) as LastPlayed
            FROM Doors d
            WHERE d.Name = @Name";

        var door = await _databaseManager.QueryFirstOrDefaultAsync<dynamic>(sql, new { Name = name });
        return door != null ? MapToDoorDto(door) : null;
    }

    public async Task<DoorDto> CreateDoorAsync(CreateDoorDto createDto, int createdBy)
    {
        const string sql = @"
            INSERT INTO Doors (Name, Description, Category, ExecutablePath, CommandLine, WorkingDirectory,
                             DropFileType, RequiresDosBox, DosBoxConfigPath, MinimumLevel, TimeLimit, DailyLimit, CreatedBy)
            VALUES (@Name, @Description, @Category, @ExecutablePath, @CommandLine, @WorkingDirectory,
                   @DropFileType, @RequiresDosBox, @DosBoxConfigPath, @MinimumLevel, @TimeLimit, @DailyLimit, @CreatedBy);
            SELECT last_insert_rowid();";

        var doorId = await _databaseManager.QueryFirstAsync<int>(sql, new
        {
            createDto.Name,
            createDto.Description,
            createDto.Category,
            createDto.ExecutablePath,
            createDto.CommandLine,
            createDto.WorkingDirectory,
            createDto.DropFileType,
            createDto.RequiresDosBox,
            createDto.DosBoxConfigPath,
            createDto.MinimumLevel,
            createDto.TimeLimit,
            createDto.DailyLimit,
            CreatedBy = createdBy
        });

        _logger.Information("Created door {DoorName} with ID {DoorId}", createDto.Name, doorId);

        var createdDoor = await GetDoorAsync(doorId);
        if (createdDoor == null) throw new InvalidOperationException($"Failed to retrieve created door with ID {doorId}");

        return createdDoor;
    }

    public async Task<DoorDto> UpdateDoorAsync(DoorDto door)
    {
        const string sql = @"
            UPDATE Doors 
            SET Name = @Name, Description = @Description, Category = @Category,
                ExecutablePath = @ExecutablePath, CommandLine = @CommandLine, WorkingDirectory = @WorkingDirectory,
                DropFileType = @DropFileType, IsActive = @IsActive, RequiresDosBox = @RequiresDosBox,
                DosBoxConfigPath = @DosBoxConfigPath, SerialPort = @SerialPort, MemorySize = @MemorySize,
                MinimumLevel = @MinimumLevel, MaximumLevel = @MaximumLevel, TimeLimit = @TimeLimit,
                DailyLimit = @DailyLimit, Cost = @Cost, SchedulingEnabled = @SchedulingEnabled,
                AvailableHours = @AvailableHours, TimeZone = @TimeZone, MultiNodeEnabled = @MultiNodeEnabled,
                MaxPlayers = @MaxPlayers, InterBbsEnabled = @InterBbsEnabled, UpdatedAt = CURRENT_TIMESTAMP
            WHERE Id = @Id";

        await _databaseManager.ExecuteAsync(sql, door);

        _logger.Information("Updated door {DoorName} (ID: {DoorId})", door.Name, door.Id);

        return (await GetDoorAsync(door.Id))!;
    }

    public async Task<bool> DeleteDoorAsync(int doorId)
    {
        var door = await GetDoorAsync(doorId);
        if (door == null) return false;

        // Check for active sessions
        var activeSessions = await GetActiveSessionsForDoorAsync(doorId);
        if (activeSessions.Any()) throw new InvalidOperationException("Cannot delete door with active sessions");

        const string sql = "UPDATE Doors SET IsActive = 0 WHERE Id = @DoorId";
        var result = await _databaseManager.ExecuteAsync(sql, new { DoorId = doorId });

        if (result > 0) _logger.Information("Deleted door {DoorName} (ID: {DoorId})", door.Name, doorId);

        return result > 0;
    }

    public async Task<IEnumerable<DoorDto>> GetDoorsByCategoryAsync(string category)
    {
        const string sql = @"
            SELECT d.*, 
                   (SELECT COUNT(*) FROM DoorSessions ds WHERE ds.DoorId = d.Id AND ds.Status = 'running') as ActiveSessions,
                   (SELECT COUNT(*) FROM DoorSessions ds WHERE ds.DoorId = d.Id) as TotalSessions,
                   (SELECT MAX(ds.StartTime) FROM DoorSessions ds WHERE ds.DoorId = d.Id) as LastPlayed
            FROM Doors d
            WHERE d.Category = @Category AND d.IsActive = 1
            ORDER BY d.Name";

        var doors = await _databaseManager.QueryAsync<dynamic>(sql, new { Category = category });
        return doors.Select(MapToDoorDto);
    }

    public async Task<IEnumerable<string>> GetDoorCategoriesAsync()
    {
        const string sql = "SELECT DISTINCT Category FROM Doors WHERE IsActive = 1 ORDER BY Category";
        return await _databaseManager.QueryAsync<string>(sql);
    }

    #endregion

    #region Door Access Control

    public async Task<bool> CanUserAccessDoorAsync(int userId, int doorId)
    {
        var door = await GetDoorAsync(doorId);
        if (door == null || !door.IsActive)
            return false;

        // Check user level
        const string userSql = "SELECT SecurityLevel FROM Users WHERE Id = @UserId";
        var userLevel = await _databaseManager.QueryFirstOrDefaultAsync<int?>(userSql, new { UserId = userId });

        if (userLevel == null || userLevel < door.MinimumLevel || userLevel > door.MaximumLevel)
            return false;

        // Check specific permissions
        const string permissionSql = @"
            SELECT AccessType FROM DoorPermissions 
            WHERE DoorId = @DoorId AND (UserId = @UserId OR UserId IS NULL)
            AND (ExpiresAt IS NULL OR ExpiresAt > CURRENT_TIMESTAMP)
            ORDER BY UserId DESC"; // User-specific permissions take precedence

        var permissions = await _databaseManager.QueryAsync<string>(permissionSql, new { DoorId = doorId, UserId = userId });

        // If there are any "deny" permissions, deny access
        if (permissions.Any(p => p == "deny"))
            return false;

        // Check daily limit
        if (await HasUserReachedDailyLimitAsync(userId, doorId))
            return false;

        // Check if door is available (scheduling, etc.)
        return await IsDoorAvailableAsync(doorId);
    }

    public async Task<bool> HasUserReachedDailyLimitAsync(int userId, int doorId)
    {
        var door = await GetDoorAsync(doorId);
        if (door == null || door.DailyLimit <= 0)
            return false;

        var dailyCount = await GetUserDailySessionCountAsync(userId, doorId);
        return dailyCount >= door.DailyLimit;
    }

    public async Task<int> GetUserDailySessionCountAsync(int userId, int doorId)
    {
        const string sql = @"
            SELECT COUNT(*) FROM DoorSessions 
            WHERE DoorId = @DoorId AND UserId = @UserId 
            AND DATE(StartTime) = DATE('now')";

        return await _databaseManager.QueryFirstAsync<int>(sql, new { DoorId = doorId, UserId = userId });
    }

    public async Task<bool> IsDoorAvailableAsync(int doorId)
    {
        var door = await GetDoorAsync(doorId);
        if (door == null || !door.IsActive)
            return false;

        // Check if executable exists
        if (!File.Exists(door.ExecutablePath))
            return false;

        // Check scheduling
        if (door.SchedulingEnabled && !string.IsNullOrEmpty(door.AvailableHours))
            if (!IsWithinScheduledHours(door.AvailableHours, door.TimeZone))
                return false;

        // Check active sessions vs max players
        if (door.MultiNodeEnabled && door.ActiveSessions >= door.MaxPlayers)
            return false;

        return true;
    }

    public async Task<string?> GetDoorUnavailableReasonAsync(int doorId)
    {
        var door = await GetDoorAsync(doorId);
        if (door == null)
            return "Door not found";

        if (!door.IsActive)
            return "Door is disabled";

        if (!File.Exists(door.ExecutablePath))
            return "Executable not found";

        if (door.SchedulingEnabled && !string.IsNullOrEmpty(door.AvailableHours))
            if (!IsWithinScheduledHours(door.AvailableHours, door.TimeZone))
                return "Door is not available at this time";

        if (door.MultiNodeEnabled && door.ActiveSessions >= door.MaxPlayers)
            return "Maximum number of players reached";

        return null;
    }

    #endregion

    #region Door Sessions

    public async Task<DoorSessionDto> StartDoorSessionAsync(int doorId, int userId, int? nodeNumber = null)
    {
        if (!await CanUserAccessDoorAsync(userId, doorId)) throw new UnauthorizedAccessException("User cannot access this door");

        var sessionId = Guid.NewGuid().ToString();

        const string sql = @"
            INSERT INTO DoorSessions (SessionId, DoorId, UserId, NodeNumber, Status)
            VALUES (@SessionId, @DoorId, @UserId, @NodeNumber, 'starting');
            SELECT last_insert_rowid();";

        var sessionDbId = await _databaseManager.QueryFirstAsync<int>(sql, new
        {
            SessionId = sessionId,
            DoorId = doorId,
            UserId = userId,
            NodeNumber = nodeNumber
        });

        await LogDoorEventAsync(doorId, "info", $"Door session started for user {userId}", sessionId: sessionDbId);

        var createdSession = await GetActiveSessionAsync(sessionId);
        if (createdSession == null) throw new InvalidOperationException($"Failed to retrieve created door session with ID {sessionId}");

        return createdSession;
    }

    public async Task<bool> EndDoorSessionAsync(string sessionId, int? exitCode = null, string? errorMessage = null)
    {
        const string sql = @"
            UPDATE DoorSessions 
            SET EndTime = CURRENT_TIMESTAMP, Status = 'completed', ExitCode = @ExitCode, ErrorMessage = @ErrorMessage
            WHERE SessionId = @SessionId";

        var result = await _databaseManager.ExecuteAsync(sql, new
        {
            SessionId = sessionId,
            ExitCode = exitCode,
            ErrorMessage = errorMessage
        });

        if (result > 0)
        {
            var session = await GetActiveSessionAsync(sessionId);
            if (session != null)
            {
                await LogDoorEventAsync(session.DoorId, "info",
                    $"Door session ended with exit code {exitCode}", sessionId: session.Id);

                // Update user statistics
                await UpdateDoorStatisticsAsync(session.DoorId, session.UserId, session.Duration);
            }

            // Cleanup drop file
            await CleanupDropFileAsync(sessionId);
        }

        return result > 0;
    }

    public async Task<DoorSessionDto?> GetActiveSessionAsync(string sessionId)
    {
        const string sql = @"
            SELECT ds.*, 
                   COALESCE(d.Name, 'Unknown Door') as DoorName, 
                   COALESCE(u.Handle, 'Unknown User') as UserHandle
            FROM DoorSessions ds
            LEFT JOIN Doors d ON ds.DoorId = d.Id
            LEFT JOIN Users u ON ds.UserId = u.Id
            WHERE ds.SessionId = @SessionId";

        _logger.Debug("Querying for session with ID: {SessionId}", sessionId);
        var session = await _databaseManager.QueryFirstOrDefaultAsync<dynamic>(sql, new { SessionId = sessionId });

        if (session == null)
        {
            _logger.Warning("No session found for ID: {SessionId}", sessionId);

            // Debug: Check if session exists without JOINs
            const string debugSql = "SELECT COUNT(*) FROM DoorSessions WHERE SessionId = @SessionId";
            var sessionCount = await _databaseManager.QueryFirstAsync<int>(debugSql, new { SessionId = sessionId });
            _logger.Debug("Session count in DoorSessions table: {Count}", sessionCount);

            return null;
        }

        return MapToDoorSessionDto(session);
    }

    public async Task<IEnumerable<DoorSessionDto>> GetActiveSessionsAsync()
    {
        const string sql = @"
            SELECT ds.*, 
                   COALESCE(d.Name, 'Unknown Door') as DoorName, 
                   COALESCE(u.Handle, 'Unknown User') as UserHandle
            FROM DoorSessions ds
            LEFT JOIN Doors d ON ds.DoorId = d.Id
            LEFT JOIN Users u ON ds.UserId = u.Id
            WHERE ds.Status IN ('starting', 'running')
            ORDER BY ds.StartTime DESC";

        var sessions = await _databaseManager.QueryAsync<dynamic>(sql);
        return sessions.Select(MapToDoorSessionDto);
    }

    public async Task<IEnumerable<DoorSessionDto>> GetActiveSessionsForDoorAsync(int doorId)
    {
        const string sql = @"
            SELECT ds.*, 
                   COALESCE(d.Name, 'Unknown Door') as DoorName, 
                   COALESCE(u.Handle, 'Unknown User') as UserHandle
            FROM DoorSessions ds
            LEFT JOIN Doors d ON ds.DoorId = d.Id
            LEFT JOIN Users u ON ds.UserId = u.Id
            WHERE ds.DoorId = @DoorId AND ds.Status IN ('starting', 'running')
            ORDER BY ds.StartTime DESC";

        var sessions = await _databaseManager.QueryAsync<dynamic>(sql, new { DoorId = doorId });
        return sessions.Select(MapToDoorSessionDto);
    }

    public async Task<IEnumerable<DoorSessionDto>> GetUserSessionHistoryAsync(int userId, int count = 50)
    {
        const string sql = @"
            SELECT ds.*, 
                   COALESCE(d.Name, 'Unknown Door') as DoorName, 
                   COALESCE(u.Handle, 'Unknown User') as UserHandle
            FROM DoorSessions ds
            LEFT JOIN Doors d ON ds.DoorId = d.Id
            LEFT JOIN Users u ON ds.UserId = u.Id
            WHERE ds.UserId = @UserId
            ORDER BY ds.StartTime DESC
            LIMIT @Count";

        var sessions = await _databaseManager.QueryAsync<dynamic>(sql, new { UserId = userId, Count = count });
        return sessions.Select(MapToDoorSessionDto);
    }

    public async Task<bool> TerminateSessionAsync(string sessionId, string reason)
    {
        const string sql = @"
            UPDATE DoorSessions 
            SET EndTime = CURRENT_TIMESTAMP, Status = 'terminated', ErrorMessage = @Reason
            WHERE SessionId = @SessionId";

        var result = await _databaseManager.ExecuteAsync(sql, new { SessionId = sessionId, Reason = reason });

        if (result > 0)
        {
            var session = await GetActiveSessionAsync(sessionId);
            if (session != null)
                await LogDoorEventAsync(session.DoorId, "warning",
                    $"Door session terminated: {reason}", sessionId: session.Id);
        }

        return result > 0;
    }

    public async Task<bool> UpdateSessionActivityAsync(string sessionId)
    {
        const string sql = @"
            UPDATE DoorSessions 
            SET LastActivity = CURRENT_TIMESTAMP, Status = 'running'
            WHERE SessionId = @SessionId";

        return await _databaseManager.ExecuteAsync(sql, new { SessionId = sessionId }) > 0;
    }

    #endregion

    #region Drop File Management

    public async Task<DropFileInfo> GenerateDropFileAsync(int doorId, int userId, string sessionId)
    {
        var door = await GetDoorAsync(doorId);
        if (door == null)
            throw new ArgumentException("Door not found", nameof(doorId));

        var template = GetDropFileTemplate(door.DropFileType);
        var dropFileName = $"{sessionId}_{door.DropFileType.ToLower().Replace(".", "_")}";
        var dropFilePath = Path.Combine(_dropFileBasePath, dropFileName);

        // Get user information
        const string userSql = @"
            SELECT Handle, FirstName, LastName, Location, SecurityLevel, TimeLeft, LastLoginAt
            FROM Users WHERE Id = @UserId";
        var user = await _databaseManager.QueryFirstOrDefaultAsync<dynamic>(userSql, new { UserId = userId });

        if (user == null)
            throw new ArgumentException("User not found", nameof(userId));

        // Use reflection to safely access properties
        var userType = user.GetType();

        // Build real name from FirstName and LastName
        var firstName = userType.GetProperty("FirstName")?.GetValue(user)?.ToString() ?? "";
        var lastName = userType.GetProperty("LastName")?.GetValue(user)?.ToString() ?? "";
        var realName = $"{firstName} {lastName}".Trim();
        if (string.IsNullOrEmpty(realName))
            realName = userType.GetProperty("Handle")?.GetValue(user)?.ToString() ?? "Unknown";

        // Replace template variables
        var variables = new Dictionary<string, string>
        {
            { "{USER_HANDLE}", userType.GetProperty("Handle")?.GetValue(user)?.ToString() ?? "Unknown" },
            { "{USER_REAL_NAME}", realName },
            { "{USER_LOCATION}", userType.GetProperty("Location")?.GetValue(user)?.ToString() ?? "Unknown" },
            { "{SECURITY_LEVEL}", userType.GetProperty("SecurityLevel")?.GetValue(user)?.ToString() ?? "0" },
            { "{TIME_LEFT}", userType.GetProperty("TimeLeft")?.GetValue(user)?.ToString() ?? "60" },
            { "{SESSION_ID}", sessionId },
            { "{DOOR_ID}", doorId.ToString() },
            { "{NODE_NUMBER}", (await GetUserNodeNumberAsync(userId)).ToString() },
            { "{BAUD_RATE}", "38400" },
            { "{COM_PORT}", door.SerialPort ?? "COM1" },
            { "{CURRENT_DATE}", DateTime.Now.ToString("MM-dd-yyyy") },
            { "{CURRENT_TIME}", DateTime.Now.ToString("HH:mm:ss") }
        };

        var content = template;
        foreach (var kvp in variables) content = content.Replace(kvp.Key, kvp.Value);

        await File.WriteAllTextAsync(dropFilePath, content);

        // Update session with drop file path
        const string updateSql = "UPDATE DoorSessions SET DropFilePath = @DropFilePath WHERE SessionId = @SessionId";
        await _databaseManager.ExecuteAsync(updateSql, new { DropFilePath = dropFilePath, SessionId = sessionId });

        return new DropFileInfo
        {
            Type = door.DropFileType,
            FileName = dropFileName,
            FilePath = dropFilePath,
            Content = content,
            Variables = variables
        };
    }

    public async Task<bool> CleanupDropFileAsync(string sessionId)
    {
        const string sql = "SELECT DropFilePath FROM DoorSessions WHERE SessionId = @SessionId";
        var dropFilePath = await _databaseManager.QueryFirstOrDefaultAsync<string>(sql, new { SessionId = sessionId });

        if (!string.IsNullOrEmpty(dropFilePath) && File.Exists(dropFilePath))
            try
            {
                File.Delete(dropFilePath);
                _logger.Debug("Cleaned up drop file {DropFilePath} for session {SessionId}", dropFilePath, sessionId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to cleanup drop file {DropFilePath}", dropFilePath);
            }

        return false;
    }

    public string GetDropFileTemplate(string dropFileType)
    {
        return dropFileType.ToUpper() switch
        {
            "DOOR.SYS" => GetDoorSysTemplate(),
            "DORINFO1.DEF" => GetDorInfo1Template(),
            _ => throw new NotSupportedException($"Drop file type {dropFileType} is not supported")
        };
    }

    public async Task<bool> ValidateDropFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return false;

            var content = await File.ReadAllTextAsync(filePath);
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // Basic validation - check if it has minimum expected lines
            return lines.Length >= 5;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Door Configuration

    public async Task<IEnumerable<DoorConfigDto>> GetDoorConfigsAsync(int doorId)
    {
        const string sql = @"
            SELECT * FROM DoorConfigs 
            WHERE DoorId = @DoorId 
            ORDER BY ConfigKey";

        var configs = await _databaseManager.QueryAsync<DoorConfigDto>(sql, new { DoorId = doorId });
        return configs;
    }

    public async Task<DoorConfigDto> SetDoorConfigAsync(int doorId, string key, string value, string type = "string")
    {
        const string upsertSql = @"
            INSERT INTO DoorConfigs (DoorId, ConfigKey, ConfigValue, ConfigType)
            VALUES (@DoorId, @ConfigKey, @ConfigValue, @ConfigType)
            ON CONFLICT(DoorId, ConfigKey) DO UPDATE SET
                ConfigValue = @ConfigValue, ConfigType = @ConfigType;
            SELECT * FROM DoorConfigs WHERE DoorId = @DoorId AND ConfigKey = @ConfigKey";

        var config = await _databaseManager.QueryFirstAsync<DoorConfigDto>(upsertSql, new
        {
            DoorId = doorId,
            ConfigKey = key,
            ConfigValue = value,
            ConfigType = type
        });

        return config;
    }

    public async Task<bool> DeleteDoorConfigAsync(int doorId, string key)
    {
        const string sql = "DELETE FROM DoorConfigs WHERE DoorId = @DoorId AND ConfigKey = @ConfigKey";
        var result = await _databaseManager.ExecuteAsync(sql, new { DoorId = doorId, ConfigKey = key });
        return result > 0;
    }

    public async Task<string?> GetDoorConfigValueAsync(int doorId, string key)
    {
        const string sql = @"
            SELECT ConfigValue FROM DoorConfigs 
            WHERE DoorId = @DoorId AND ConfigKey = @ConfigKey";

        return await _databaseManager.QueryFirstOrDefaultAsync<string>(sql, new { DoorId = doorId, ConfigKey = key });
    }

    #endregion

    #region Statistics and Monitoring

    public async Task<DoorSystemStatisticsDto> GetDoorSystemStatisticsAsync()
    {
        const string sql = @"
            SELECT 
                COUNT(DISTINCT d.Id) as TotalDoors,
                COUNT(DISTINCT CASE WHEN d.IsActive = 1 THEN d.Id END) as ActiveDoors,
                COUNT(DISTINCT CASE WHEN ds.Status IN ('starting', 'running') THEN ds.SessionId END) as ActiveSessions,
                COUNT(DISTINCT CASE WHEN DATE(ds.StartTime) = DATE('now') THEN ds.SessionId END) as TotalSessionsToday,
                COALESCE(SUM(CASE WHEN DATE(ds.StartTime) = DATE('now') AND ds.EndTime IS NOT NULL 
                    THEN (julianday(ds.EndTime) - julianday(ds.StartTime)) * 24 * 3600 ELSE 0 END), 0) as TotalPlayTimeToday,
                COUNT(DISTINCT CASE WHEN DATE(ds.StartTime) = DATE('now') THEN ds.UserId END) as UniquePlayersToday
            FROM Doors d
            LEFT JOIN DoorSessions ds ON d.Id = ds.DoorId";

        var stats = await _databaseManager.QueryFirstAsync<DoorSystemStatisticsDto>(sql);

        // Get most played doors
        stats.MostPlayedDoors = (await GetMostPlayedDoorsAsync(5)).ToList();

        // Get recent sessions
        stats.RecentSessions = (await GetRecentSessionsAsync(10)).ToList();

        // Get active categories
        stats.ActiveCategories = (await GetDoorCategoriesAsync()).ToList();

        return stats;
    }

    public async Task<IEnumerable<DoorStatisticsDto>> GetDoorStatisticsAsync(int? doorId = null, int? userId = null)
    {
        var whereConditions = new List<string>();
        var parameters = new Dictionary<string, object>();

        if (doorId.HasValue)
        {
            whereConditions.Add("dst.DoorId = @DoorId");
            parameters["DoorId"] = doorId.Value;
        }

        if (userId.HasValue)
        {
            whereConditions.Add("dst.UserId = @UserId");
            parameters["UserId"] = userId.Value;
        }

        var whereClause = whereConditions.Any() ? "WHERE " + string.Join(" AND ", whereConditions) : "";

        var sql = $@"
            SELECT dst.*, d.Name as DoorName, u.Handle as UserHandle
            FROM DoorStatistics dst
            INNER JOIN Doors d ON dst.DoorId = d.Id
            INNER JOIN Users u ON dst.UserId = u.Id
            {whereClause}
            ORDER BY dst.TotalSessions DESC";

        var stats = await _databaseManager.QueryAsync<DoorStatisticsDto>(sql, parameters);
        return stats;
    }

    public async Task<DoorStatisticsDto?> GetUserDoorStatisticsAsync(int doorId, int userId)
    {
        const string sql = @"
            SELECT dst.*, d.Name as DoorName, u.Handle as UserHandle
            FROM DoorStatistics dst
            INNER JOIN Doors d ON dst.DoorId = d.Id
            INNER JOIN Users u ON dst.UserId = u.Id
            WHERE dst.DoorId = @DoorId AND dst.UserId = @UserId";

        return await _databaseManager.QueryFirstOrDefaultAsync<DoorStatisticsDto>(sql,
            new { DoorId = doorId, UserId = userId });
    }

    public async Task<bool> UpdateDoorStatisticsAsync(int doorId, int userId, int sessionTime, int? score = null)
    {
        const string upsertSql = @"
            INSERT INTO DoorStatistics (DoorId, UserId, TotalSessions, TotalTime, LastPlayed, HighScore)
            VALUES (@DoorId, @UserId, 1, @SessionTime, CURRENT_TIMESTAMP, @Score)
            ON CONFLICT(DoorId, UserId) DO UPDATE SET
                TotalSessions = TotalSessions + 1,
                TotalTime = TotalTime + @SessionTime,
                LastPlayed = CURRENT_TIMESTAMP,
                HighScore = CASE WHEN @Score IS NOT NULL AND (@Score > HighScore OR HighScore IS NULL) 
                           THEN @Score ELSE HighScore END,
                UpdatedAt = CURRENT_TIMESTAMP";

        var result = await _databaseManager.ExecuteAsync(upsertSql, new
        {
            DoorId = doorId,
            UserId = userId,
            SessionTime = sessionTime,
            Score = score
        });

        return result > 0;
    }

    public async Task<IEnumerable<DoorDto>> GetMostPlayedDoorsAsync(int count = 10)
    {
        const string sql = @"
            SELECT d.*, 
                   COUNT(ds.Id) as TotalSessions,
                   (SELECT COUNT(*) FROM DoorSessions ds2 WHERE ds2.DoorId = d.Id AND ds2.Status = 'running') as ActiveSessions,
                   MAX(ds.StartTime) as LastPlayed
            FROM Doors d
            LEFT JOIN DoorSessions ds ON d.Id = ds.DoorId
            WHERE d.IsActive = 1
            GROUP BY d.Id
            ORDER BY COUNT(ds.Id) DESC
            LIMIT @Count";

        var doors = await _databaseManager.QueryAsync<dynamic>(sql, new { Count = count });
        return doors.Select(MapToDoorDto);
    }

    public async Task<IEnumerable<DoorSessionDto>> GetRecentSessionsAsync(int count = 20)
    {
        const string sql = @"
            SELECT ds.*, 
                   COALESCE(d.Name, 'Unknown Door') as DoorName, 
                   COALESCE(u.Handle, 'Unknown User') as UserHandle
            FROM DoorSessions ds
            LEFT JOIN Doors d ON ds.DoorId = d.Id
            LEFT JOIN Users u ON ds.UserId = u.Id
            ORDER BY ds.StartTime DESC
            LIMIT @Count";

        var sessions = await _databaseManager.QueryAsync<dynamic>(sql, new { Count = count });
        return sessions.Select(MapToDoorSessionDto);
    }

    #endregion

    #region DOSBox Integration

    public bool IsDosBoxAvailable()
    {
        try
        {
            var dosBoxExecutable = FindDosBoxExecutable();
            return !string.IsNullOrEmpty(dosBoxExecutable) && File.Exists(dosBoxExecutable);
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> GenerateDosBoxConfigAsync(int doorId, string sessionId)
    {
        var door = await GetDoorAsync(doorId);
        if (door == null)
            throw new ArgumentException("Door not found", nameof(doorId));

        var configContent = new StringBuilder();
        configContent.AppendLine("[sdl]");
        configContent.AppendLine("windowresolution=800x600");
        configContent.AppendLine("output=overlay");
        configContent.AppendLine("");

        configContent.AppendLine("[dosbox]");
        configContent.AppendLine($"memsize={door.MemorySize}");
        configContent.AppendLine("");

        configContent.AppendLine("[serial]");
        configContent.AppendLine($"serial1=nullmodem server:localhost port:23{sessionId.GetHashCode() % 1000:D3}");
        configContent.AppendLine("");

        configContent.AppendLine("[autoexec]");
        configContent.AppendLine("@echo off");
        if (!string.IsNullOrEmpty(door.WorkingDirectory))
        {
            configContent.AppendLine($"mount c \"{door.WorkingDirectory}\"");
            configContent.AppendLine("c:");
        }

        var configPath = Path.Combine(_dropFileBasePath, $"{sessionId}_dosbox.conf");
        await File.WriteAllTextAsync(configPath, configContent.ToString());

        return configPath;
    }

    public async Task<Process?> StartDosBoxSessionAsync(int doorId, string sessionId, string dropFilePath)
    {
        var door = await GetDoorAsync(doorId);
        if (door == null || !door.RequiresDosBox)
            return null;

        var dosBoxExecutable = FindDosBoxExecutable();
        if (string.IsNullOrEmpty(dosBoxExecutable))
            throw new InvalidOperationException("DOSBox executable not found");

        var configPath = await GenerateDosBoxConfigAsync(doorId, sessionId);

        var processStartInfo = new ProcessStartInfo
        {
            FileName = dosBoxExecutable,
            Arguments = $"-conf \"{configPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        try
        {
            var process = Process.Start(processStartInfo);
            if (process != null)
            {
                // Update session with process ID
                const string sql = "UPDATE DoorSessions SET ProcessId = @ProcessId WHERE SessionId = @SessionId";
                await _databaseManager.ExecuteAsync(sql, new { ProcessId = process.Id, SessionId = sessionId });

                _logger.Information("Started DOSBox process {ProcessId} for door {DoorId} session {SessionId}",
                    process.Id, doorId, sessionId);
            }

            return process;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to start DOSBox for door {DoorId} session {SessionId}", doorId, sessionId);
            throw;
        }
    }

    public async Task<bool> ValidateDosBoxInstallationAsync()
    {
        try
        {
            var dosBoxExecutable = FindDosBoxExecutable();
            if (string.IsNullOrEmpty(dosBoxExecutable) || !File.Exists(dosBoxExecutable))
                return false;

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = dosBoxExecutable,
                Arguments = "-version",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            });

            if (process != null)
            {
                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Permissions

    public async Task<IEnumerable<DoorPermissionDto>> GetDoorPermissionsAsync(int doorId)
    {
        const string sql = @"
            SELECT dp.*, u.Handle as UserHandle, gu.Handle as GrantedByHandle
            FROM DoorPermissions dp
            LEFT JOIN Users u ON dp.UserId = u.Id
            LEFT JOIN Users gu ON dp.GrantedBy = gu.Id
            WHERE dp.DoorId = @DoorId
            ORDER BY dp.GrantedAt DESC";

        return await _databaseManager.QueryAsync<DoorPermissionDto>(sql, new { DoorId = doorId });
    }

    public async Task<DoorPermissionDto> AddDoorPermissionAsync(int doorId, int? userId, string? userGroup,
        string accessType, int grantedBy, DateTime? expiresAt = null)
    {
        const string sql = @"
            INSERT INTO DoorPermissions (DoorId, UserId, UserGroup, AccessType, GrantedBy, ExpiresAt)
            VALUES (@DoorId, @UserId, @UserGroup, @AccessType, @GrantedBy, @ExpiresAt);
            SELECT last_insert_rowid();";

        var permissionId = await _databaseManager.QueryFirstAsync<int>(sql, new
        {
            DoorId = doorId,
            UserId = userId,
            UserGroup = userGroup,
            AccessType = accessType,
            GrantedBy = grantedBy,
            ExpiresAt = expiresAt
        });

        const string selectSql = @"
            SELECT dp.*, u.Handle as UserHandle, gu.Handle as GrantedByHandle
            FROM DoorPermissions dp
            LEFT JOIN Users u ON dp.UserId = u.Id
            LEFT JOIN Users gu ON dp.GrantedBy = gu.Id
            WHERE dp.Id = @PermissionId";

        return await _databaseManager.QueryFirstAsync<DoorPermissionDto>(selectSql, new { PermissionId = permissionId });
    }

    public async Task<bool> RemoveDoorPermissionAsync(int permissionId)
    {
        const string sql = "DELETE FROM DoorPermissions WHERE Id = @PermissionId";
        var result = await _databaseManager.ExecuteAsync(sql, new { PermissionId = permissionId });
        return result > 0;
    }

    public async Task<bool> CheckUserPermissionAsync(int doorId, int userId)
    {
        return await CanUserAccessDoorAsync(userId, doorId);
    }

    #endregion

    #region Maintenance

    public async Task<bool> TestDoorExecutableAsync(int doorId)
    {
        var door = await GetDoorAsync(doorId);
        if (door == null)
            return false;

        return File.Exists(door.ExecutablePath);
    }

    public async Task<IEnumerable<string>> ValidateDoorConfigurationAsync(int doorId)
    {
        var issues = new List<string>();
        var door = await GetDoorAsync(doorId);

        if (door == null)
        {
            issues.Add("Door not found");
            return issues;
        }

        if (!File.Exists(door.ExecutablePath))
            issues.Add("Executable file not found");

        if (!string.IsNullOrEmpty(door.WorkingDirectory) && !Directory.Exists(door.WorkingDirectory))
            issues.Add("Working directory not found");

        if (door.RequiresDosBox && !IsDosBoxAvailable())
            issues.Add("DOSBox not available but required");

        if (!string.IsNullOrEmpty(door.DosBoxConfigPath) && !File.Exists(door.DosBoxConfigPath))
            issues.Add("DOSBox config file not found");

        return issues;
    }

    public async Task<int> CleanupExpiredSessionsAsync()
    {
        // Clean up sessions that have been running for more than the time limit + grace period
        const string sql = @"
            UPDATE DoorSessions 
            SET Status = 'expired', EndTime = CURRENT_TIMESTAMP
            WHERE Status IN ('starting', 'running') 
            AND datetime(StartTime, '+' || (
                SELECT TimeLimit FROM Doors WHERE Id = DoorSessions.DoorId
            ) || ' minutes', '+15 minutes') < datetime('now')";

        var result = await _databaseManager.ExecuteAsync(sql);

        if (result > 0) _logger.Information("Cleaned up {Count} expired door sessions", result);

        return result;
    }

    public async Task<int> CleanupOrphanedFilesAsync()
    {
        var dropFiles = Directory.GetFiles(_dropFileBasePath, "*");
        var deletedCount = 0;

        foreach (var filePath in dropFiles)
        {
            var fileName = Path.GetFileName(filePath);

            // Extract session ID from filename
            var sessionIdMatch = Regex.Match(fileName, @"^([a-f0-9\-]+)_");
            if (sessionIdMatch.Success)
            {
                var sessionId = sessionIdMatch.Groups[1].Value;

                // Check if session still exists and is not completed
                const string sql = @"
                    SELECT COUNT(*) FROM DoorSessions 
                    WHERE SessionId = @SessionId AND Status IN ('starting', 'running')";

                var activeCount = await _databaseManager.QueryFirstAsync<int>(sql, new { SessionId = sessionId });

                if (activeCount == 0)
                    try
                    {
                        File.Delete(filePath);
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "Failed to delete orphaned file {FilePath}", filePath);
                    }
            }
        }

        if (deletedCount > 0) _logger.Information("Cleaned up {Count} orphaned door files", deletedCount);

        return deletedCount;
    }

    public async Task<bool> BackupDoorDataAsync(int doorId, string backupPath)
    {
        // This would implement door-specific data backup
        // For now, just a placeholder
        await Task.Delay(1);
        return true;
    }

    public async Task<bool> RestoreDoorDataAsync(int doorId, string backupPath)
    {
        // This would implement door-specific data restoration
        // For now, just a placeholder
        await Task.Delay(1);
        return true;
    }

    #endregion

    #region Logging

    public async Task LogDoorEventAsync(int doorId, string level, string message, string? details = null, int? sessionId = null)
    {
        const string sql = @"
            INSERT INTO DoorLogs (DoorId, SessionId, LogLevel, Message, Details)
            VALUES (@DoorId, @SessionId, @LogLevel, @Message, @Details)";

        await _databaseManager.ExecuteAsync(sql, new
        {
            DoorId = doorId,
            SessionId = sessionId,
            LogLevel = level,
            Message = message,
            Details = details
        });

        // Also log to main logger
        switch (level.ToLower())
        {
            case "debug":
                _logger.Debug("Door {DoorId}: {Message}", doorId, message);
                break;
            case "info":
                _logger.Information("Door {DoorId}: {Message}", doorId, message);
                break;
            case "warning":
                _logger.Warning("Door {DoorId}: {Message}", doorId, message);
                break;
            case "error":
                _logger.Error("Door {DoorId}: {Message}", doorId, message);
                break;
        }
    }

    public async Task<IEnumerable<DoorLog>> GetDoorLogsAsync(int? doorId = null, int? sessionId = null, string? level = null, int count = 100)
    {
        var whereConditions = new List<string>();
        var parameters = new Dictionary<string, object> { { "Count", count } };

        if (doorId.HasValue)
        {
            whereConditions.Add("DoorId = @DoorId");
            parameters["DoorId"] = doorId.Value;
        }

        if (sessionId.HasValue)
        {
            whereConditions.Add("SessionId = @SessionId");
            parameters["SessionId"] = sessionId.Value;
        }

        if (!string.IsNullOrEmpty(level))
        {
            whereConditions.Add("LogLevel = @Level");
            parameters["Level"] = level;
        }

        var whereClause = whereConditions.Any() ? "WHERE " + string.Join(" AND ", whereConditions) : "";

        var sql = $@"
            SELECT * FROM DoorLogs 
            {whereClause}
            ORDER BY Timestamp DESC 
            LIMIT @Count";

        return await _databaseManager.QueryAsync<DoorLog>(sql, parameters);
    }

    #endregion

    #region Private Helper Methods

    private DoorDto MapToDoorDto(dynamic data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        // Handle ExpandoObject (used in tests) and regular objects differently
        if (data is IDictionary<string, object> expandoData)
            // ExpandoObject case - used in unit tests
            return new DoorDto
            {
                Id = GetExpandoProperty<int>(expandoData, "Id"),
                Name = GetExpandoProperty<string>(expandoData, "Name") ?? string.Empty,
                Description = GetExpandoProperty<string>(expandoData, "Description"),
                Category = GetExpandoProperty<string>(expandoData, "Category") ?? string.Empty,
                ExecutablePath = GetExpandoProperty<string>(expandoData, "ExecutablePath") ?? string.Empty,
                CommandLine = GetExpandoProperty<string>(expandoData, "CommandLine"),
                WorkingDirectory = GetExpandoProperty<string>(expandoData, "WorkingDirectory"),
                DropFileType = GetExpandoProperty<string>(expandoData, "DropFileType") ?? "DOOR.SYS",
                DropFileLocation = GetExpandoProperty<string>(expandoData, "DropFileLocation"),
                IsActive = GetExpandoProperty<bool>(expandoData, "IsActive"),
                RequiresDosBox = GetExpandoProperty<bool>(expandoData, "RequiresDosBox"),
                DosBoxConfigPath = GetExpandoProperty<string>(expandoData, "DosBoxConfigPath"),
                SerialPort = GetExpandoProperty<string>(expandoData, "SerialPort") ?? "COM1",
                MemorySize = GetExpandoProperty<int>(expandoData, "MemorySize"),
                MinimumLevel = GetExpandoProperty<int>(expandoData, "MinimumLevel"),
                MaximumLevel = GetExpandoProperty<int>(expandoData, "MaximumLevel"),
                TimeLimit = GetExpandoProperty<int>(expandoData, "TimeLimit"),
                DailyLimit = GetExpandoProperty<int>(expandoData, "DailyLimit"),
                Cost = GetExpandoProperty<int>(expandoData, "Cost"),
                SchedulingEnabled = GetExpandoProperty<bool>(expandoData, "SchedulingEnabled"),
                AvailableHours = GetExpandoProperty<string>(expandoData, "AvailableHours"),
                TimeZone = GetExpandoProperty<string>(expandoData, "TimeZone"),
                MultiNodeEnabled = GetExpandoProperty<bool>(expandoData, "MultiNodeEnabled"),
                MaxPlayers = GetExpandoProperty<int>(expandoData, "MaxPlayers"),
                InterBbsEnabled = GetExpandoProperty<bool>(expandoData, "InterBbsEnabled"),
                CreatedAt = GetExpandoProperty<DateTime>(expandoData, "CreatedAt"),
                UpdatedAt = GetExpandoProperty<DateTime>(expandoData, "UpdatedAt"),
                CreatedBy = GetExpandoProperty<int?>(expandoData, "CreatedBy"),
                ActiveSessions = GetExpandoProperty<int>(expandoData, "ActiveSessions"),
                TotalSessions = GetExpandoProperty<int>(expandoData, "TotalSessions"),
                LastPlayed = GetExpandoProperty<DateTime?>(expandoData, "LastPlayed"),
                IsAvailable = true, // Will be calculated dynamically
                UnavailableReason = null
            };

        // Regular object case - use reflection
        var dataType = data.GetType();

        return new DoorDto
        {
            Id = Convert.ToInt32(dataType.GetProperty("Id")?.GetValue(data) ?? 0),
            Name = dataType.GetProperty("Name")?.GetValue(data)?.ToString() ?? string.Empty,
            Description = dataType.GetProperty("Description")?.GetValue(data)?.ToString(),
            Category = dataType.GetProperty("Category")?.GetValue(data)?.ToString() ?? string.Empty,
            ExecutablePath = dataType.GetProperty("ExecutablePath")?.GetValue(data)?.ToString() ?? string.Empty,
            CommandLine = dataType.GetProperty("CommandLine")?.GetValue(data)?.ToString(),
            WorkingDirectory = dataType.GetProperty("WorkingDirectory")?.GetValue(data)?.ToString(),
            DropFileType = dataType.GetProperty("DropFileType")?.GetValue(data)?.ToString() ?? "DOOR.SYS",
            DropFileLocation = dataType.GetProperty("DropFileLocation")?.GetValue(data)?.ToString(),
            IsActive = Convert.ToBoolean(dataType.GetProperty("IsActive")?.GetValue(data) ?? false),
            RequiresDosBox = Convert.ToBoolean(dataType.GetProperty("RequiresDosBox")?.GetValue(data) ?? false),
            DosBoxConfigPath = dataType.GetProperty("DosBoxConfigPath")?.GetValue(data)?.ToString(),
            SerialPort = dataType.GetProperty("SerialPort")?.GetValue(data)?.ToString() ?? "COM1",
            MemorySize = Convert.ToInt32(dataType.GetProperty("MemorySize")?.GetValue(data) ?? 16),
            MinimumLevel = Convert.ToInt32(dataType.GetProperty("MinimumLevel")?.GetValue(data) ?? 0),
            MaximumLevel = Convert.ToInt32(dataType.GetProperty("MaximumLevel")?.GetValue(data) ?? 255),
            TimeLimit = Convert.ToInt32(dataType.GetProperty("TimeLimit")?.GetValue(data) ?? 60),
            DailyLimit = Convert.ToInt32(dataType.GetProperty("DailyLimit")?.GetValue(data) ?? 5),
            Cost = Convert.ToInt32(dataType.GetProperty("Cost")?.GetValue(data) ?? 0),
            SchedulingEnabled = Convert.ToBoolean(dataType.GetProperty("SchedulingEnabled")?.GetValue(data) ?? false),
            AvailableHours = dataType.GetProperty("AvailableHours")?.GetValue(data)?.ToString(),
            TimeZone = dataType.GetProperty("TimeZone")?.GetValue(data)?.ToString(),
            MultiNodeEnabled = Convert.ToBoolean(dataType.GetProperty("MultiNodeEnabled")?.GetValue(data) ?? false),
            MaxPlayers = Convert.ToInt32(dataType.GetProperty("MaxPlayers")?.GetValue(data) ?? 1),
            InterBbsEnabled = Convert.ToBoolean(dataType.GetProperty("InterBbsEnabled")?.GetValue(data) ?? false),
            CreatedAt = Convert.ToDateTime(dataType.GetProperty("CreatedAt")?.GetValue(data) ?? DateTime.UtcNow),
            UpdatedAt = Convert.ToDateTime(dataType.GetProperty("UpdatedAt")?.GetValue(data) ?? DateTime.UtcNow),
            CreatedBy = dataType.GetProperty("CreatedBy")?.GetValue(data) as int?,
            ActiveSessions = Convert.ToInt32(dataType.GetProperty("ActiveSessions")?.GetValue(data) ?? 0),
            TotalSessions = Convert.ToInt32(dataType.GetProperty("TotalSessions")?.GetValue(data) ?? 0),
            LastPlayed = dataType.GetProperty("LastPlayed")?.GetValue(data) as DateTime?,
            IsAvailable = true, // Will be calculated dynamically
            UnavailableReason = null
        };
    }

    private T GetExpandoProperty<T>(IDictionary<string, object> data, string propertyName)
    {
        if (data.TryGetValue(propertyName, out var value) && value != null)
        {
            if (value is T directValue)
                return directValue;

            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return default!;
            }
        }

        return default!;
    }

    private DoorSessionDto MapToDoorSessionDto(dynamic data)
    {
        if (data == null)
        {
            _logger.Warning("MapToDoorSessionDto received null data");
            throw new ArgumentNullException(nameof(data), "Door session data cannot be null");
        }

        // Use reflection to safely access properties
        var dataType = data.GetType();

        var endTime = dataType.GetProperty("EndTime")?.GetValue(data) as DateTime?;
        var startTime = Convert.ToDateTime(dataType.GetProperty("StartTime")?.GetValue(data) ?? DateTime.UtcNow);

        return new DoorSessionDto
        {
            Id = Convert.ToInt32(dataType.GetProperty("Id")?.GetValue(data) ?? 0),
            SessionId = dataType.GetProperty("SessionId")?.GetValue(data)?.ToString() ?? string.Empty,
            DoorId = Convert.ToInt32(dataType.GetProperty("DoorId")?.GetValue(data) ?? 0),
            DoorName = dataType.GetProperty("DoorName")?.GetValue(data)?.ToString() ?? string.Empty,
            UserId = Convert.ToInt32(dataType.GetProperty("UserId")?.GetValue(data) ?? 0),
            UserHandle = dataType.GetProperty("UserHandle")?.GetValue(data)?.ToString() ?? string.Empty,
            NodeNumber = dataType.GetProperty("NodeNumber")?.GetValue(data) as int?,
            StartTime = startTime,
            EndTime = endTime,
            Duration = endTime != null ? (int)(endTime.Value - startTime).TotalSeconds : (int)(DateTime.UtcNow - startTime).TotalSeconds,
            ExitCode = dataType.GetProperty("ExitCode")?.GetValue(data) as int?,
            Status = dataType.GetProperty("Status")?.GetValue(data)?.ToString() ?? string.Empty,
            ErrorMessage = dataType.GetProperty("ErrorMessage")?.GetValue(data)?.ToString(),
            LastActivity = Convert.ToDateTime(dataType.GetProperty("LastActivity")?.GetValue(data) ?? DateTime.UtcNow)
        };
    }

    private string FindDosBoxExecutable()
    {
        var possiblePaths = new[]
        {
            _dosBoxPath,
            "dosbox",
            "/usr/bin/dosbox",
            "/usr/local/bin/dosbox",
            "C:\\Program Files\\DOSBox\\dosbox.exe",
            "C:\\Program Files (x86)\\DOSBox\\dosbox.exe"
        };

        foreach (var path in possiblePaths)
            if (File.Exists(path))
                return path;

        return string.Empty;
    }

    private bool IsWithinScheduledHours(string availableHours, string? timeZone)
    {
        try
        {
            var parts = availableHours.Split('-');
            if (parts.Length != 2)
                return true; // Invalid format, allow access

            var startTime = TimeSpan.Parse(parts[0]);
            var endTime = TimeSpan.Parse(parts[1]);

            var tz = string.IsNullOrEmpty(timeZone) ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(timeZone);
            var currentTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).TimeOfDay;

            if (startTime <= endTime) return currentTime >= startTime && currentTime <= endTime;

            // Spans midnight
            return currentTime >= startTime || currentTime <= endTime;
        }
        catch
        {
            return true; // Error parsing, allow access
        }
    }

    private string GetDoorSysTemplate()
    {
        return @"COM{COM_PORT}:
{BAUD_RATE}
8
{NODE_NUMBER}
{SESSION_ID}
{USER_HANDLE}
{USER_REAL_NAME}
{USER_LOCATION}
{SECURITY_LEVEL}
{TIME_LEFT}
-1
{CURRENT_DATE}
{CURRENT_TIME}
{CURRENT_TIME}
9999
1
01-01-99
00:00
00:00
999
1
0
1
Y
Y
Y
7
0
1
1200
{USER_HANDLE}";
    }

    private string GetDorInfo1Template()
    {
        return @"{USER_HANDLE}
{USER_REAL_NAME}
{USER_LOCATION}
{SECURITY_LEVEL}
{TIME_LEFT}
{BAUD_RATE}
{NODE_NUMBER}";
    }

    private async Task<int> GetUserNodeNumberAsync(int userId)
    {
        try
        {
            // Assign node numbers based on active session order
            // This could be enhanced to use a dedicated node management system
            const string sql = @"
                SELECT ROW_NUMBER() OVER (ORDER BY CreatedAt) as NodeNumber
                FROM UserSessions 
                WHERE IsActive = 1 AND ExpiresAt > @Now AND UserId = @UserId
                ORDER BY CreatedAt
                LIMIT 1";

            var nodeNumber = await _databaseManager.QueryFirstOrDefaultAsync<int?>(sql,
                new { UserId = userId, Now = DateTime.UtcNow });

            return nodeNumber ?? 1; // Default to node 1 if not found
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting node number for user {UserId}", userId);
            return 1; // Default fallback
        }
    }

    #endregion
}