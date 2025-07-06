using System.Reflection;
using Blackboard.Core.Configuration;
using Blackboard.Core.Logging;
using Blackboard.Core.Network;
using Blackboard.Core.Services;
using Blackboard.Data;
using Blackboard.Data.Configuration;
using Blackboard.UI;
using CommandLine;
using Serilog;

namespace Blackboard;

internal class Program
{
    private static ILogger? _logger;
    private static ConfigurationManager? _configManager;
    private static DatabaseManager? _databaseManager;
    private static TelnetServer? _telnetServer;

    private static async Task Main(string[] args)
    {
        var startTime = DateTime.UtcNow;
        CommandLineOptions? options = null;

        // Parse command line arguments
        var result = Parser.Default.ParseArguments<CommandLineOptions>(args);
        await result.WithParsedAsync(async opts =>
        {
            options = opts;

            // Handle version request
            if (opts.Version)
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                Console.WriteLine($"Blackboard v{version}");
                Environment.Exit(0);
                return;
            }

            await RunApplication(opts, startTime);
        });

        // If parsing failed, exit
        if (options == null) Environment.Exit(1);
    }

    private static async Task RunApplication(CommandLineOptions options, DateTime startTime)
    {
        try
        {
            // Initialize configuration
            var configPath = options.ConfigPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "blackboard.yml");
            _configManager = new ConfigurationManager(configPath, Log.Logger);
            var rootPath = PathResolver.ResolveRootPath(_configManager.Configuration.System.RootPath);

            // Initialize logging (with verbose option support)
            var config = _configManager.Configuration;
            if (options.Verbose)
                // Override logging level to verbose if specified
                config.Logging.LogLevel = "Verbose";

            _logger = LoggingConfiguration.CreateLogger(config);
            Log.Logger = _logger;

            _logger.Information("Starting Blackboard...");
            _logger.Information("Configuration loaded from {ConfigPath}", configPath);

            // Initialize database
            _logger.Information("Root path set to {RootPath}", rootPath);

            // Ensure the rootPath directory exists
            if (!Directory.Exists(rootPath))
            {
                _logger.Information("Creating root directory at {RootPath}", rootPath);
                Directory.CreateDirectory(rootPath);
            }

            var databaseConfig = new DatabaseConfiguration
            {
                ConnectionString = PathResolver.ResolveConnectionString(
                    _configManager.Configuration.Database.ConnectionString,
                    rootPath),
                EnableWalMode = _configManager.Configuration.Database.EnableWalMode,
                ConnectionTimeoutSeconds = _configManager.Configuration.Database.ConnectionTimeoutSeconds,
                EnableBackup = _configManager.Configuration.Database.EnableBackup,
                BackupPath = PathResolver.ResolvePath(
                    ConfigurationManager.DatabaseBackupPath,
                    rootPath)
            };

            // Ensure database directory exists
            var dataSourcePath = databaseConfig.ConnectionString.Replace("Data Source=", "");
            var dbPath = Path.GetDirectoryName(dataSourcePath);
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
                var backupDir = PathResolver.ResolvePath(ConfigurationManager.DatabaseBackupPath, rootPath);
                if (!Directory.Exists(backupDir))
                {
                    _logger.Information("Creating backup directory at {BackupDir}", backupDir);
                    Directory.CreateDirectory(backupDir);
                }
            }

            // Initialize core services required for telnet server
            var passwordService = new PasswordService();
            var sessionService = new SessionService(_databaseManager, _logger);
            var auditService = new AuditService(_databaseManager, _logger);
            var userService = new UserService(_databaseManager, passwordService, sessionService, auditService,
                _configManager.Configuration.Security, _logger);
            var messageService = new MessageService(_databaseManager);

            // Resolve files path using configuration
            var filesPath = PathResolver.ResolvePath(ConfigurationManager.FilesPath, rootPath);
            var fileAreaService = new FileAreaService(_databaseManager, _logger, filesPath);

            // Initialize Phase 7 ANSI screen services - resolve screens path using configuration
            var screensPath = PathResolver.ResolvePath(ConfigurationManager.ScreensPath, rootPath);
            _logger.Information("Resolved screens path: {ScreensPath}", screensPath);
            var templateProcessor = new TemplateVariableProcessor(_logger, _databaseManager);
            var ansiScreenService = new AnsiScreenService(screensPath, _logger, templateProcessor);
            var screenSequenceService = new ScreenSequenceService(ansiScreenService, _logger);
            var keyboardHandler = new KeyboardHandlerService(_logger);

            // Initialize telnet server with screensDir and new services
            var telnetConfig = _configManager.Configuration.Network;
            if (options.Port.HasValue)
            {
                telnetConfig.TelnetPort = options.Port.Value;
                _logger.Information("Overriding telnet port to {Port}", options.Port.Value);
            }

            _telnetServer = new TelnetServer(_logger, _configManager, userService, sessionService, messageService, fileAreaService, ansiScreenService, screenSequenceService, keyboardHandler, screensPath);

            // Check if we should auto-start the terminal server
            var shouldStartServer = !options.NoServer && _configManager.Configuration.System.TerminalServerAutoStart;

            if (shouldStartServer)
            {
                _logger.Information("Starting telnet server...");
                await _telnetServer.StartAsync();
            }
            else
            {
                _logger.Information("Telnet server auto-start is disabled.");
            }

            var initTime = DateTime.UtcNow - startTime;
            _logger.Information("Blackboard initialized in {InitTime:F2} seconds", initTime.TotalSeconds);

            // Decide whether to run in console mode or GUI mode
            if (options.ConsoleMode)
            {
                _logger.Information("Running in console mode");
                await RunConsoleMode();
            }
            else
            {
                _logger.Information("Running in GUI mode");
                await RunGuiMode();
            }
        }
        catch (Exception ex)
        {
            if (_logger != null)
                _logger.Fatal(ex, "Fatal error during startup");
            else
                Console.WriteLine($"Fatal error during startup: {ex}");

            Environment.ExitCode = 1;
        }
        finally
        {
            await Shutdown().ConfigureAwait(false);
        }
    }

    private static async Task RunConsoleMode()
    {
        _logger?.Information("Console mode started. Press Ctrl+C to exit.");

        // Set up cancellation token for graceful shutdown
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            // Keep the application running until cancelled
            await Task.Delay(Timeout.Infinite, cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger?.Information("Console mode shutdown requested");
        }
    }

    private static async Task RunGuiMode()
    {
        // Create and run the main application UI
        var mainApp = new MainApplication(_logger!, _configManager!, _databaseManager!, _telnetServer!);

        // Run the Terminal.Gui application
        await Task.Run(() => mainApp.Run());
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
            await Log.CloseAndFlushAsync();
        }
    }
}