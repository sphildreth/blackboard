namespace Blackboard.Core.Configuration;

public class SystemConfiguration
{
    public SystemSettings System { get; set; } = new();
    public NetworkSettings Network { get; set; } = new();
    public SecuritySettings Security { get; set; } = new();
    public DatabaseSettings Database { get; set; } = new();
    public LoggingSettings Logging { get; set; } = new();
}

public class SystemSettings
{
    public string BoardName { get; set; } = "Blackboard BBS";
    public string SysopName { get; set; } = "System Operator";
    public string Location { get; set; } = "Somewhere, USA";
    public bool SystemOnline { get; set; } = false;
    public bool RequirePreEnterCode { get; set; } = false;
    public string PreEnterCode { get; set; } = string.Empty;
    public int MaxUsers { get; set; } = 100;
    public string TimeZone { get; set; } = "UTC";
}

public class NetworkSettings
{
    public string TelnetBindAddress { get; set; } = "0.0.0.0";
    public int TelnetPort { get; set; } = 23;
    public int MaxConcurrentConnections { get; set; } = 10;
    public int ConnectionTimeoutSeconds { get; set; } = 300;
    public bool EnableSsh { get; set; } = false;
    public int SshPort { get; set; } = 22;
}

public class SecuritySettings
{
    public int MaxLoginAttempts { get; set; } = 3;
    public int LockoutDurationMinutes { get; set; } = 30;
    public int PasswordMinLength { get; set; } = 8;
    public bool RequirePasswordComplexity { get; set; } = true;
    public int PasswordExpirationDays { get; set; } = 90;
    public bool EnableAuditLogging { get; set; } = true;
    public bool EnableEncryption { get; set; } = true;
}

public class DatabaseSettings
{
    public string ConnectionString { get; set; } = "Data Source=blackboard.db";
    public bool EnableWalMode { get; set; } = true;
    public int ConnectionTimeoutSeconds { get; set; } = 30;
    public bool EnableBackup { get; set; } = true;
    public string BackupPath { get; set; } = "backups";
}

public class LoggingSettings
{
    public string LogLevel { get; set; } = "Information";
    public string LogPath { get; set; } = "logs";
    public int MaxLogFileSizeMB { get; set; } = 100;
    public int RetainedLogFiles { get; set; } = 7;
    public bool EnableConsoleLogging { get; set; } = true;
    public bool EnableFileLogging { get; set; } = true;
}
