using Blackboard.Data.Configuration;

namespace Blackboard.Core.Configuration;

public class DatabaseSettings : IDatabaseConfiguration
{
    public string ConnectionString { get; set; } = "Data Source=blackboard.db";
    public bool EnableWalMode { get; set; } = true;
    public int ConnectionTimeoutSeconds { get; set; } = 30;
    public bool EnableBackup { get; set; } = true;
}