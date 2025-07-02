namespace Blackboard.Core.DTOs;

public class SystemStatisticsDto
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int UsersOnlineToday { get; set; }
    public int TotalSessions { get; set; }
    public int ActiveSessions { get; set; }
    public int TotalCalls { get; set; }
    public int CallsToday { get; set; }
    public int RegistrationsToday { get; set; }
    public int MessagesToday { get; set; }
    public int FilesDownloadedToday { get; set; }
    public int PeakUsersToday { get; set; }
    public DateTime? FirstOnlineDate { get; set; }
    public TimeSpan SystemUptime { get; set; }
}

public class DashboardStatisticsDto
{
    public SystemStatisticsDto SystemStats { get; set; } = new();
    public IEnumerable<ActiveSessionDto> ActiveSessions { get; set; } = new List<ActiveSessionDto>();
    public IEnumerable<SystemAlertDto> SystemAlerts { get; set; } = new List<SystemAlertDto>();
    public SystemResourcesDto SystemResources { get; set; } = new();
    public DatabaseStatusDto DatabaseStatus { get; set; } = new();
}

public class ActiveSessionDto
{
    public string SessionId { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;
    public string? RealName { get; set; }
    public string? Location { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public DateTime LoginTime { get; set; }
    public DateTime LastActivity { get; set; }
    public string CurrentActivity { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public TimeSpan SessionDuration { get; set; }
    public bool IsActive { get; set; }
}

public class SystemAlertDto
{
    public AlertType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public AlertSeverity Severity { get; set; }
    public bool IsAcknowledged { get; set; }
    public string? Details { get; set; }
}

public enum AlertType
{
    System,
    Security,
    Database,
    Network,
    Resource,
    UserActivity,
    Error
}

public enum AlertSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

public class SystemResourcesDto
{
    public double CpuUsagePercent { get; set; }
    public long MemoryUsedBytes { get; set; }
    public long MemoryTotalBytes { get; set; }
    public double MemoryUsagePercent { get; set; }
    public long DiskUsedBytes { get; set; }
    public long DiskTotalBytes { get; set; }
    public double DiskUsagePercent { get; set; }
    public int ActiveConnections { get; set; }
    public int MaxConnections { get; set; }
}

public class DatabaseStatusDto
{
    public bool IsConnected { get; set; }
    public string ConnectionString { get; set; } = string.Empty;
    public DateTime? LastBackup { get; set; }
    public string DatabaseVersion { get; set; } = string.Empty;
    public long DatabaseSizeBytes { get; set; }
    public int ActiveConnections { get; set; }
    public bool WalModeEnabled { get; set; }
    public string? LastError { get; set; }
}
