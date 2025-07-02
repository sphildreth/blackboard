using System.Diagnostics;
using System.Text.Json;
using Blackboard.Core.DTOs;
using Blackboard.Data;
using Blackboard.Data.Configuration;
using Serilog;

namespace Blackboard.Core.Services;

public class SystemStatisticsService : ISystemStatisticsService
{
    private readonly IDatabaseManager _database;
    private readonly IDatabaseConfiguration _config;
    private readonly ILogger _logger;
    private readonly DateTime _systemStartTime;

    public SystemStatisticsService(IDatabaseManager database, IDatabaseConfiguration config, ILogger logger)
    {
        _database = database;
        _config = config;
        _logger = logger;
        _systemStartTime = DateTime.UtcNow;
    }

    public async Task<SystemStatisticsDto> GetSystemStatisticsAsync()
    {
        try
        {
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);

            // Get total users
            var totalUsers = await _database.QueryFirstOrDefaultAsync<int>(
                "SELECT COUNT(*) FROM Users WHERE IsActive = 1");

            // Get active sessions
            var activeSessions = await _database.QueryFirstOrDefaultAsync<int>(
                "SELECT COUNT(*) FROM UserSessions WHERE IsActive = 1 AND ExpiresAt > @Now",
                new { Now = DateTime.UtcNow });

            // Get users online today
            var usersOnlineToday = await _database.QueryFirstOrDefaultAsync<int>(@"
                SELECT COUNT(DISTINCT UserId) 
                FROM UserSessions 
                WHERE CreatedAt >= @Today AND CreatedAt < @Tomorrow",
                new { Today = today, Tomorrow = tomorrow });

            // Get total sessions
            var totalSessions = await _database.QueryFirstOrDefaultAsync<int>(
                "SELECT COUNT(*) FROM UserSessions");

            // Get registrations today
            var registrationsToday = await _database.QueryFirstOrDefaultAsync<int>(@"
                SELECT COUNT(*) 
                FROM Users 
                WHERE CreatedAt >= @Today AND CreatedAt < @Tomorrow",
                new { Today = today, Tomorrow = tomorrow });

            // Get first online date (first user registration)
            var firstOnlineDateResult = await _database.QueryFirstOrDefaultAsync<DateTime?>(@"
                SELECT MIN(CreatedAt) FROM Users WHERE IsActive = 1");

            // Calculate peak users today (max concurrent sessions)
            var peakUsersToday = await CalculatePeakUsersToday(today, tomorrow);

            return new SystemStatisticsDto
            {
                TotalUsers = totalUsers,
                ActiveUsers = await GetActiveUsersCount(),
                UsersOnlineToday = usersOnlineToday,
                TotalSessions = totalSessions,
                ActiveSessions = activeSessions,
                TotalCalls = totalSessions, // Total calls = total sessions
                CallsToday = usersOnlineToday, // Calls today = users online today
                RegistrationsToday = registrationsToday,
                MessagesToday = 0, // TODO: Implement when messaging is added
                FilesDownloadedToday = 0, // TODO: Implement when file system is added
                PeakUsersToday = peakUsersToday,
                FirstOnlineDate = firstOnlineDateResult,
                SystemUptime = DateTime.UtcNow - _systemStartTime
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting system statistics");
            return new SystemStatisticsDto();
        }
    }

    public async Task<DashboardStatisticsDto> GetDashboardStatisticsAsync()
    {
        try
        {
            var systemStats = await GetSystemStatisticsAsync();
            var activeSessions = await GetActiveSessionsAsync();
            var systemAlerts = await GetSystemAlertsAsync();
            var systemResources = await GetSystemResourcesAsync();
            var databaseStatus = await GetDatabaseStatusAsync();

            return new DashboardStatisticsDto
            {
                SystemStats = systemStats,
                ActiveSessions = activeSessions,
                SystemAlerts = systemAlerts,
                SystemResources = systemResources,
                DatabaseStatus = databaseStatus
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting dashboard statistics");
            return new DashboardStatisticsDto();
        }
    }

    public async Task<IEnumerable<ActiveSessionDto>> GetActiveSessionsAsync()
    {
        try
        {
            const string sql = @"
                SELECT 
                    s.Id as SessionId,
                    u.Handle,
                    u.FirstName || ' ' || u.LastName as RealName,
                    u.Location,
                    s.IpAddress,
                    s.CreatedAt as LoginTime,
                    s.CreatedAt as LastActivity,
                    s.UserAgent,
                    s.IsActive
                FROM UserSessions s
                INNER JOIN Users u ON s.UserId = u.Id
                WHERE s.IsActive = 1 AND s.ExpiresAt > @Now
                ORDER BY s.CreatedAt DESC";

            var sessions = await _database.QueryAsync<dynamic>(sql, new { Now = DateTime.UtcNow });
            var now = DateTime.UtcNow;

            return sessions.Select(s => new ActiveSessionDto
            {
                SessionId = s.SessionId,
                Handle = s.Handle,
                RealName = !string.IsNullOrWhiteSpace(s.RealName) && s.RealName.Trim() != " " ? s.RealName.Trim() : null,
                Location = s.Location,
                IpAddress = s.IpAddress,
                LoginTime = DateTime.Parse(s.LoginTime.ToString()),
                LastActivity = DateTime.Parse(s.LastActivity.ToString()),
                CurrentActivity = "Online", // TODO: Track actual activity when implemented
                UserAgent = s.UserAgent,
                SessionDuration = now - DateTime.Parse(s.LoginTime.ToString()),
                IsActive = Convert.ToBoolean(s.IsActive)
            });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting active sessions");
            return new List<ActiveSessionDto>();
        }
    }

    public async Task<IEnumerable<SystemAlertDto>> GetSystemAlertsAsync()
    {
        try
        {
            var alerts = new List<SystemAlertDto>();

            // Check for recent failed login attempts
            var recentFailedLogins = await _database.QueryFirstOrDefaultAsync<int>(@"
                SELECT COUNT(*) 
                FROM AuditLogs 
                WHERE Action = 'USER_LOGIN_FAILED' 
                AND CreatedAt > @Since",
                new { Since = DateTime.UtcNow.AddMinutes(-30) });

            if (recentFailedLogins > 5)
            {
                alerts.Add(new SystemAlertDto
                {
                    Type = AlertType.Security,
                    Severity = AlertSeverity.Warning,
                    Title = "Multiple Failed Login Attempts",
                    Message = $"{recentFailedLogins} failed login attempts in the last 30 minutes",
                    Timestamp = DateTime.UtcNow,
                    IsAcknowledged = false
                });
            }

            // Check for locked users
            var lockedUsers = await _database.QueryFirstOrDefaultAsync<int>(@"
                SELECT COUNT(*) 
                FROM Users 
                WHERE LockedUntil > @Now",
                new { Now = DateTime.UtcNow });

            if (lockedUsers > 0)
            {
                alerts.Add(new SystemAlertDto
                {
                    Type = AlertType.Security,
                    Severity = AlertSeverity.Info,
                    Title = "Locked User Accounts",
                    Message = $"{lockedUsers} user accounts are currently locked",
                    Timestamp = DateTime.UtcNow,
                    IsAcknowledged = false
                });
            }

            // Check system resources
            var resources = await GetSystemResourcesAsync();
            if (resources.MemoryUsagePercent > 80)
            {
                alerts.Add(new SystemAlertDto
                {
                    Type = AlertType.Resource,
                    Severity = AlertSeverity.Warning,
                    Title = "High Memory Usage",
                    Message = $"Memory usage is at {resources.MemoryUsagePercent:F1}%",
                    Timestamp = DateTime.UtcNow,
                    IsAcknowledged = false
                });
            }

            if (resources.DiskUsagePercent > 90)
            {
                alerts.Add(new SystemAlertDto
                {
                    Type = AlertType.Resource,
                    Severity = AlertSeverity.Critical,
                    Title = "Disk Space Critical",
                    Message = $"Disk usage is at {resources.DiskUsagePercent:F1}%",
                    Timestamp = DateTime.UtcNow,
                    IsAcknowledged = false
                });
            }

            return alerts.OrderByDescending(a => a.Severity).ThenByDescending(a => a.Timestamp);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting system alerts");
            return new List<SystemAlertDto>();
        }
    }

    public async Task<SystemResourcesDto> GetSystemResourcesAsync()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            
            // Get memory usage
            var memoryUsed = process.WorkingSet64;
            var totalMemory = GC.GetTotalMemory(false);
            
            // Get system memory info (approximation)
            var systemMemory = GetSystemMemory();
            
            // Get disk space for database directory
            var dbPath = await GetDatabasePath();
            var driveInfo = new DriveInfo(Path.GetPathRoot(dbPath) ?? "/");
            
            // Get active connections (sessions)
            var activeConnections = await _database.QueryFirstOrDefaultAsync<int>(
                "SELECT COUNT(*) FROM UserSessions WHERE IsActive = 1 AND ExpiresAt > @Now",
                new { Now = DateTime.UtcNow });

            return new SystemResourcesDto
            {
                CpuUsagePercent = GetCpuUsage(),
                MemoryUsedBytes = memoryUsed,
                MemoryTotalBytes = systemMemory,
                MemoryUsagePercent = (double)memoryUsed / systemMemory * 100,
                DiskUsedBytes = driveInfo.TotalSize - driveInfo.AvailableFreeSpace,
                DiskTotalBytes = driveInfo.TotalSize,
                DiskUsagePercent = (double)(driveInfo.TotalSize - driveInfo.AvailableFreeSpace) / driveInfo.TotalSize * 100,
                ActiveConnections = activeConnections,
                MaxConnections = 10 // TODO: Get from configuration
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting system resources");
            return new SystemResourcesDto();
        }
    }

    public async Task<DatabaseStatusDto> GetDatabaseStatusAsync()
    {
        try
        {
            var isConnected = await TestDatabaseConnection();
            var dbPath = await GetDatabasePath();
            var dbFileInfo = new FileInfo(dbPath);
            
            // Get database version
            var version = await _database.QueryFirstOrDefaultAsync<string>("SELECT sqlite_version()") ?? "Unknown";
            
            // Check WAL mode
            var walMode = await _database.QueryFirstOrDefaultAsync<string>("PRAGMA journal_mode") ?? "Unknown";
            
            return new DatabaseStatusDto
            {
                IsConnected = isConnected,
                ConnectionString = _config.ConnectionString,
                LastBackup = null, // TODO: Implement backup tracking
                DatabaseVersion = version,
                DatabaseSizeBytes = dbFileInfo.Exists ? dbFileInfo.Length : 0,
                ActiveConnections = 1, // SQLite doesn't have concurrent connections like other DBs
                WalModeEnabled = walMode.Equals("wal", StringComparison.OrdinalIgnoreCase),
                LastError = null
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting database status");
            return new DatabaseStatusDto
            {
                IsConnected = false,
                LastError = ex.Message
            };
        }
    }

    private async Task<int> GetActiveUsersCount()
    {
        return await _database.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(DISTINCT UserId) FROM UserSessions WHERE IsActive = 1 AND ExpiresAt > @Now",
            new { Now = DateTime.UtcNow });
    }

    private async Task<int> CalculatePeakUsersToday(DateTime today, DateTime tomorrow)
    {
        // This is a simplified calculation - in a real system, you'd track this over time
        return await _database.QueryFirstOrDefaultAsync<int>(@"
            SELECT COUNT(DISTINCT UserId)
            FROM UserSessions 
            WHERE CreatedAt >= @Today AND CreatedAt < @Tomorrow",
            new { Today = today, Tomorrow = tomorrow });
    }

    private async Task<bool> TestDatabaseConnection()
    {
        try
        {
            await _database.QueryFirstOrDefaultAsync<int>("SELECT 1");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private Task<string> GetDatabasePath()
    {
        try
        {
            // Extract path from connection string
            var connectionString = _config.ConnectionString;
            var dataSourceIndex = connectionString.IndexOf("Data Source=", StringComparison.OrdinalIgnoreCase);
            if (dataSourceIndex >= 0)
            {
                var pathStart = dataSourceIndex + "Data Source=".Length;
                var pathEnd = connectionString.IndexOf(';', pathStart);
                if (pathEnd < 0) pathEnd = connectionString.Length;
                return Task.FromResult(connectionString.Substring(pathStart, pathEnd - pathStart));
            }
            return Task.FromResult("/tmp/unknown.db");
        }
        catch
        {
            return Task.FromResult("/tmp/unknown.db");
        }
    }

    private long GetSystemMemory()
    {
        try
        {
            // On Linux, read from /proc/meminfo
            if (File.Exists("/proc/meminfo"))
            {
                var lines = File.ReadAllLines("/proc/meminfo");
                var memTotalLine = lines.FirstOrDefault(l => l.StartsWith("MemTotal:"));
                if (memTotalLine != null)
                {
                    var parts = memTotalLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && long.TryParse(parts[1], out var kb))
                    {
                        return kb * 1024; // Convert KB to bytes
                    }
                }
            }
            
            // Fallback to GC memory (not accurate for system memory)
            return GC.GetTotalMemory(false) * 4; // Rough estimate
        }
        catch
        {
            return GC.GetTotalMemory(false) * 4;
        }
    }

    private double GetCpuUsage()
    {
        try
        {
            var process = Process.GetCurrentProcess();
            // This is a simplified CPU usage calculation
            // In a real system, you'd want to track CPU time over intervals
            return Math.Min(process.TotalProcessorTime.TotalMilliseconds / Environment.TickCount * 100, 100);
        }
        catch
        {
            return 0;
        }
    }
}
