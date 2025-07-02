using Serilog;
using Blackboard.Core.Configuration;
using Blackboard.Core.Logging;
using Blackboard.Core.Network;
using Blackboard.Data;
using Blackboard.UI;

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
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "blackboard.yml");
            _configManager = new ConfigurationManager(configPath, Log.Logger);

            // Initialize logging
            _logger = LoggingConfiguration.CreateLogger(_configManager.Configuration);
            Log.Logger = _logger;

            _logger.Information("Starting Blackboard BBS...");
            _logger.Information("Configuration loaded from {ConfigPath}", configPath);

            // Initialize database
            var databaseConfig = new Blackboard.Data.Configuration.DatabaseConfiguration
            {
                ConnectionString = _configManager.Configuration.Database.ConnectionString,
                EnableWalMode = _configManager.Configuration.Database.EnableWalMode,
                ConnectionTimeoutSeconds = _configManager.Configuration.Database.ConnectionTimeoutSeconds,
                EnableBackup = _configManager.Configuration.Database.EnableBackup,
                BackupPath = _configManager.Configuration.Database.BackupPath
            };
            _databaseManager = new DatabaseManager(_logger, databaseConfig);
            await _databaseManager.InitializeAsync();

            // Initialize telnet server
            _telnetServer = new TelnetServer(_logger, _configManager);

            // Check if we should auto-start the server
            if (_configManager.Configuration.System.SystemOnline)
            {
                _logger.Information("System configured to start online, starting telnet server...");
                await _telnetServer.StartAsync();
            }

            // Create and run the main application UI
            var mainApp = new MainApplication(_logger, _configManager, _databaseManager, _telnetServer);

            var initTime = DateTime.UtcNow - startTime;
            _logger.Information("Blackboard BBS initialized in {InitTime:F2} seconds", initTime.TotalSeconds);

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
        _logger?.Information("Shutting down Blackboard BBS...");

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

            _logger?.Information("Blackboard BBS shutdown complete");
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
