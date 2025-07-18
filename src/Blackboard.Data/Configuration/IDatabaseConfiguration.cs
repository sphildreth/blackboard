namespace Blackboard.Data.Configuration;

public interface IDatabaseConfiguration
{
    string ConnectionString { get; }
    bool EnableWalMode { get; }
    int ConnectionTimeoutSeconds { get; }
    bool EnableBackup { get; }
}

public class DatabaseConfiguration : IDatabaseConfiguration
{
    public string BackupPath { get; set; } = "backups";
    public string ConnectionString { get; set; } = "Data Source=data/blackboard.db";
    public bool EnableWalMode { get; set; } = true;
    public int ConnectionTimeoutSeconds { get; set; } = 30;
    public bool EnableBackup { get; set; } = true;
}