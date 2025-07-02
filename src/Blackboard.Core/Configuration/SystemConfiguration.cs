namespace Blackboard.Core.Configuration;

public class SystemConfiguration
{
    public SystemSettings System { get; set; } = new();
    public NetworkSettings Network { get; set; } = new();
    public SecuritySettings Security { get; set; } = new();
    public DatabaseSettings Database { get; set; } = new();
    public LoggingSettings Logging { get; set; } = new();
}