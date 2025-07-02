using Serilog;
using Blackboard.Core;
using Blackboard.Core.Configuration;
using Blackboard.Core.Logging;
using Blackboard.Core.Network;
using Blackboard.Data;
using Blackboard.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Blackboard;

class Program
{
    private static ILogger? _logger;
    private static ConfigurationManager? _configManager;
    private static DatabaseManager? _databaseManager;
    private static TelnetServer? _telnetServer;

    static async Task Main(string[] args)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            // Initialize configuration
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "blackboard.yml");
            _configManager = new ConfigurationManager(configPath, Log.Logger);

            // Initialize logging
            _logger = LoggingConfiguration.CreateLogger(_configManager.Configuration);
            Log.Logger = _logger;

            _logger.Information("Starting Blackboard...");
            _logger.Information("Configuration loaded from {ConfigPath}", configPath);

            // Initialize database
            string rootPath = _configManager.Configuration.System.RootPath;
            
            // Ensure the rootPath directory exists
            if (!Directory.Exists(rootPath))
            {
                _logger.Information("Creating root directory at {RootPath}", rootPath);
                Directory.CreateDirectory(rootPath);
            }
            
            var databaseConfig = new Blackboard.Data.Configuration.DatabaseConfiguration
            {
                ConnectionString = PathResolver.ResolveConnectionString(
                    _configManager.Configuration.Database.ConnectionString, 
                    rootPath),
                EnableWalMode = _configManager.Configuration.Database.EnableWalMode,
                ConnectionTimeoutSeconds = _configManager.Configuration.Database.ConnectionTimeoutSeconds,
                EnableBackup = _configManager.Configuration.Database.EnableBackup,
                BackupPath = PathResolver.ResolvePath(
                    _configManager.Configuration.Database.BackupPath,
                    rootPath)
            };
            
            // Ensure database directory exists
            string dataSourcePath = databaseConfig.ConnectionString.Replace("Data Source=", "");
            string? dbPath = Path.GetDirectoryName(dataSourcePath);
            if (!string.IsNullOrEmpty(dbPath) && !Directory.Exists(dbPath))
            {
                _logger.Information("Creating database directory at {DbPath}", dbPath);
                Directory.CreateDirectory(dbPath);
            }
            
            _databaseManager = new DatabaseManager(_logger, databaseConfig);
            await _databaseManager.InitializeAsync();

            // Set up backup directory if backup is enabled
            if (_configManager.Configuration.Database.EnableBackup)
            {
                string backupDir = PathResolver.ResolvePath(_configManager.Configuration.Database.BackupPath, rootPath);
                if (!Directory.Exists(backupDir))
                {
                    _logger.Information("Creating backup directory at {BackupDir}", backupDir);
                    Directory.CreateDirectory(backupDir);
                }
            }
            
            // Initialize telnet server
            _telnetServer = new TelnetServer(_logger, _configManager);

            // Check if we should auto-start the terminal server
            if (_configManager.Configuration.System.TerminalServerAutoStart)
            {
                _logger.Information("Terminal server auto-start is enabled, starting telnet server...");
                await _telnetServer.StartAsync();
            }
            else
            {
                _logger.Information("Terminal server auto-start is disabled by configuration.");
            }

            // Create and run the main application UI
            var mainApp = new MainApplication(_logger, _configManager, _databaseManager, _telnetServer);

            var initTime = DateTime.UtcNow - startTime;
            _logger.Information("Blackboard initialized in {InitTime:F2} seconds", initTime.TotalSeconds);

            // Run the Terminal.Gui application
            mainApp.Run();
        }
        catch (Exception ex)
        {
            if (_logger != null)
            {
                _logger.Fatal(ex, "Fatal error during startup");
            }
            else
            {
                Console.WriteLine($"Fatal error during startup: {ex}");
            }

            Environment.ExitCode = 1;
        }
        finally
        {
            await Shutdown().ConfigureAwait(false);
        }
    }

    private static async Task Shutdown()
    {
        _logger?.Information("Shutting down Blackboard...");

        try
        {
            // Stop telnet server
            if (_telnetServer != null)
            {
                await _telnetServer.StopAsync();
                _logger?.Information("Telnet server stopped");
            }

            // Close database connection
            if (_databaseManager != null)
            {
                await _databaseManager.CloseAsync();
                _databaseManager.Dispose();
                _logger?.Information("Database connection closed");
            }

            // Dispose configuration manager
            _configManager?.Dispose();

            _logger?.Information("Blackboard shutdown complete");
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Error during shutdown");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
