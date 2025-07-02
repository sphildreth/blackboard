namespace Blackboard.Core.Configuration;

public class LoggingSettings
{
    public string LogLevel { get; set; } = "Information";
    public string LogPath { get; set; } = "logs";
    public int MaxLogFileSizeMB { get; set; } = 100;
    public int RetainedLogFiles { get; set; } = 7;
    public bool EnableConsoleLogging { get; set; } = true;
    public bool EnableFileLogging { get; set; } = true;
}