using Blackboard.Core.Configuration;
using Blackboard.Data;
using Serilog;
using Serilog.Events;

namespace Blackboard.Core.Logging;

public static class LoggingConfiguration
{
    public static ILogger CreateLogger(SystemConfiguration configuration)
    {
        var loggingConfig = configuration.Logging;
        var rootPath = PathResolver.ResolveRootPath(configuration.System.RootPath);

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Is(ParseLogLevel(loggingConfig.LogLevel))
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "Blackboard")
            .Enrich.WithEnvironmentName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId();

        // Configure console logging
        if (loggingConfig.EnableConsoleLogging)
            loggerConfig = loggerConfig.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");

        // Configure file logging
        if (loggingConfig.EnableFileLogging)
        {
            // Resolve and ensure log directory exists
            var resolvedLogPath = PathResolver.ResolvePath(ConfigurationManager.LogsPath, rootPath);
            if (!Directory.Exists(resolvedLogPath)) Directory.CreateDirectory(resolvedLogPath);

            var logFilePath = Path.Combine(resolvedLogPath, "blackboard-.log");

            loggerConfig = loggerConfig.WriteTo.File(
                logFilePath,
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: loggingConfig.MaxLogFileSizeMB * 1024 * 1024,
                retainedFileCountLimit: loggingConfig.RetainedLogFiles,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");

            // Separate error log
            var errorLogPath = Path.Combine(resolvedLogPath, "blackboard-errors-.log");
            loggerConfig = loggerConfig.WriteTo.File(
                errorLogPath,
                LogEventLevel.Error,
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: loggingConfig.MaxLogFileSizeMB * 1024 * 1024,
                retainedFileCountLimit: loggingConfig.RetainedLogFiles,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
        }

        return loggerConfig.CreateLogger();
    }

    private static LogEventLevel ParseLogLevel(string logLevel)
    {
        return logLevel.ToLowerInvariant() switch
        {
            "verbose" => LogEventLevel.Verbose,
            "debug" => LogEventLevel.Debug,
            "information" => LogEventLevel.Information,
            "warning" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information
        };
    }

    public static void ConfigureForDatabase(ILogger logger, DatabaseManager databaseManager)
    {
        // This would be implemented to write logs to database
        // For now, we'll keep it simple with file/console logging
    }
}