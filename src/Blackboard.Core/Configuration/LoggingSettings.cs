namespace Blackboard.Core.Configuration;

public class LoggingSettings
{
    public string LogLevel { get; set; } = "Information";
    public int MaxLogFileSizeMB { get; set; } = 100;
    public int RetainedLogFiles { get; set; } = 7;
    public bool EnableConsoleLogging { get; set; } = false;
    public bool EnableFileLogging { get; set; } = true;
}