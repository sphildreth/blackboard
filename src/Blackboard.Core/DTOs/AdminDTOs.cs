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

// File Management DTOs
public class FileAreaDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Path { get; set; } = string.Empty;
    public int RequiredLevel { get; set; }
    public int UploadLevel { get; set; }
    public bool IsActive { get; set; }
    public long MaxFileSize { get; set; }
    public bool AllowUploads { get; set; }
    public bool AllowDownloads { get; set; }
    public int FileCount { get; set; }
    public long TotalSize { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class BbsFileDto
{
    public int Id { get; set; }
    public int AreaId { get; set; }
    public string AreaName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public long Size { get; set; }
    public string SizeFormatted { get; set; } = string.Empty;
    public string? MimeType { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public DateTime UploadDate { get; set; }
    public int? UploaderId { get; set; }
    public string? UploaderHandle { get; set; }
    public int DownloadCount { get; set; }
    public DateTime? LastDownloadAt { get; set; }
    public bool IsApproved { get; set; }
    public int? ApprovedBy { get; set; }
    public string? ApproverHandle { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public bool IsActive { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public double AverageRating { get; set; }
    public int RatingCount { get; set; }
    public string Checksum { get; set; } = string.Empty;
}

public class FileRatingDto
{
    public int Id { get; set; }
    public int FileId { get; set; }
    public int UserId { get; set; }
    public string UserHandle { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime RatingDate { get; set; }
}

public class FileSearchResultDto
{
    public List<BbsFileDto> Files { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasNextPage => (Page * PageSize) < TotalCount;
    public bool HasPreviousPage => Page > 1;
}

public class FileUploadDto
{
    public int AreaId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string[] Tags { get; set; } = Array.Empty<string>();
    public byte[] FileData { get; set; } = Array.Empty<byte>();
    public string? MimeType { get; set; }
}

public class FileAreaStatisticsDto
{
    public int TotalAreas { get; set; }
    public int ActiveAreas { get; set; }
    public int TotalFiles { get; set; }
    public int ApprovedFiles { get; set; }
    public int PendingApproval { get; set; }
    public long TotalFileSize { get; set; }
    public int DownloadsToday { get; set; }
    public int UploadsToday { get; set; }
    public List<FileAreaDto> MostActiveAreas { get; set; } = new();
    public List<BbsFileDto> MostDownloadedFiles { get; set; } = new();
    public List<BbsFileDto> RecentUploads { get; set; } = new();
}

// Door Game System DTOs
public class DoorDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string? CommandLine { get; set; }
    public string? WorkingDirectory { get; set; }
    public string DropFileType { get; set; } = "DOOR.SYS";
    public string? DropFileLocation { get; set; }
    public bool IsActive { get; set; }
    public bool RequiresDosBox { get; set; }
    public string? DosBoxConfigPath { get; set; }
    public string? SerialPort { get; set; }
    public int MemorySize { get; set; }
    public int MinimumLevel { get; set; }
    public int MaximumLevel { get; set; }
    public int TimeLimit { get; set; }
    public int DailyLimit { get; set; }
    public int Cost { get; set; }
    public bool SchedulingEnabled { get; set; }
    public string? AvailableHours { get; set; }
    public string? TimeZone { get; set; }
    public bool MultiNodeEnabled { get; set; }
    public int MaxPlayers { get; set; }
    public bool InterBbsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? CreatedBy { get; set; }

    // Additional runtime info
    public int ActiveSessions { get; set; }
    public int TotalSessions { get; set; }
    public DateTime? LastPlayed { get; set; }
    public bool IsAvailable { get; set; }
    public string? UnavailableReason { get; set; }
}

public class DoorSessionDto
{
    public int Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public int DoorId { get; set; }
    public string DoorName { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string UserHandle { get; set; } = string.Empty;
    public int? NodeNumber { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int Duration { get; set; }
    public int? ExitCode { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime LastActivity { get; set; }
}

public class DoorStatisticsDto
{
    public int Id { get; set; }
    public int DoorId { get; set; }
    public string DoorName { get; set; } = string.Empty;
    public int UserId { get; set; }
    public string UserHandle { get; set; } = string.Empty;
    public int TotalSessions { get; set; }
    public int TotalTime { get; set; }
    public DateTime? LastPlayed { get; set; }
    public int? HighScore { get; set; }
    public string? HighScoreData { get; set; }
}

public class DoorSystemStatisticsDto
{
    public int TotalDoors { get; set; }
    public int ActiveDoors { get; set; }
    public int ActiveSessions { get; set; }
    public int TotalSessions { get; set; }
    public int TotalSessionsToday { get; set; }
    public int TotalSessionTime { get; set; } // total time in seconds
    public int TotalPlayTimeToday { get; set; } // seconds
    public int UniquePlayersToday { get; set; }
    public List<DoorDto> MostPlayedDoors { get; set; } = new();
    public List<DoorSessionDto> RecentSessions { get; set; } = new();
    public List<string> ActiveCategories { get; set; } = new();
}

public class DoorConfigDto
{
    public int Id { get; set; }
    public int DoorId { get; set; }
    public string ConfigKey { get; set; } = string.Empty;
    public string? ConfigValue { get; set; }
    public string ConfigType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class DoorPermissionDto
{
    public int Id { get; set; }
    public int DoorId { get; set; }
    public int? UserId { get; set; }
    public string? UserHandle { get; set; }
    public string? UserGroup { get; set; }
    public string AccessType { get; set; } = string.Empty;
    public DateTime GrantedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int? GrantedBy { get; set; }
    public string? GrantedByHandle { get; set; }
}

public class CreateDoorDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string? CommandLine { get; set; }
    public string? WorkingDirectory { get; set; }
    public string DropFileType { get; set; } = "DOOR.SYS";
    public string? DropFileLocation { get; set; }
    public bool RequiresDosBox { get; set; }
    public string? DosBoxConfigPath { get; set; }
    public string? SerialPort { get; set; }
    public int MemorySize { get; set; } = 640;
    public int MinimumLevel { get; set; }
    public int MaximumLevel { get; set; } = 999;
    public int TimeLimit { get; set; } = 60;
    public int DailyLimit { get; set; } = 5;
    public int Cost { get; set; } = 0;
    public bool SchedulingEnabled { get; set; }
    public string? AvailableHours { get; set; }
    public string? TimeZone { get; set; }
    public bool MultiNodeEnabled { get; set; }
    public int MaxPlayers { get; set; } = 1;
    public bool InterBbsEnabled { get; set; }
}

public class DropFileInfo
{
    public string Type { get; set; } = string.Empty; // DOOR.SYS, DORINFO1.DEF, etc.
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Dictionary<string, string> Variables { get; set; } = new();
}

public class FossilEmulationDto
{
    public string SessionId { get; set; } = string.Empty;
    public string ComPort { get; set; } = "COM1";
    public int BaudRate { get; set; } = 38400;
    public int DataBits { get; set; } = 8;
    public int StopBits { get; set; } = 1;
    public string Parity { get; set; } = "None";
    public string NamedPipeName { get; set; } = string.Empty;
    public string PipeName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public long BytesSent { get; set; }
    public long BytesReceived { get; set; }
}
