using System.Collections.ObjectModel;
using Blackboard.Core.Configuration;
using Blackboard.Core.Network;
using Blackboard.Core.Services;
using Blackboard.Data;
using Blackboard.UI.Admin;
using Serilog;
using Terminal.Gui.App;
using Terminal.Gui.Configuration;
using Terminal.Gui.Views;
using ConfigurationManager = Terminal.Gui.Configuration.ConfigurationManager;

namespace Blackboard.UI;

public class MainApplication
{
    private readonly IAuditService _auditService;
    private readonly Core.Configuration.ConfigurationManager _configManager;
    private readonly DatabaseManager _databaseManager;
    private readonly IFileAreaService _fileAreaService;
    private readonly ILogger _logger;
    private readonly ISystemStatisticsService _statisticsService;
    private readonly TelnetServer _telnetServer;

    // Core services for admin functionality
    private readonly IUserService _userService;
    private Label? _connectionsLabel;
    private ListView? _connectionsListView;

    private Window? _mainWindow;
    private Button? _startStopButton;
    private Label? _statusLabel;
    private Label? _uptimeLabel;

    public MainApplication(ILogger logger, Core.Configuration.ConfigurationManager configManager,
        DatabaseManager databaseManager, TelnetServer telnetServer)
    {
        _logger = logger;
        _configManager = configManager;
        _databaseManager = databaseManager;
        _telnetServer = telnetServer;

        // Initialize core services for admin functionality
        var passwordService = new PasswordService();
        var sessionService = new SessionService(databaseManager, logger);
        _auditService = new AuditService(databaseManager, logger);
        _userService = new UserService(databaseManager, passwordService, sessionService, _auditService,
            configManager.Configuration.Security, logger);
        _statisticsService = new SystemStatisticsService(databaseManager, configManager.Configuration.Database, logger);

        // Resolve files path using configuration - same as in Program.cs and ServiceManager
        var rootPath = configManager.Configuration.System.RootPath;
        var filesPath = PathResolver.ResolvePath(Core.Configuration.ConfigurationManager.FilesPath, rootPath);
        _fileAreaService = new FileAreaService(databaseManager, logger, filesPath);
    }

    public void Run()
    {
        try
        {
            Application.Init();

            // Enable Terminal.Gui's ConfigurationManager for theme support
            ConfigurationManager.Enable(ConfigLocations.All);

            // Apply the configured theme
            ThemeManager.ApplyTheme(_configManager.Configuration.System.Theme);

            CreateMainWindow();
            SetupEventHandlers();
            UpdateDisplay();
            Application.AddTimeout(TimeSpan.FromSeconds(1), () =>
            {
                UpdateDisplay();
                return true;
            });
            Application.Run(_mainWindow!);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Terminal UI failed to start, this may be due to running in a headless environment");
            Console.WriteLine("Blackboard is running in console mode.");
            Console.WriteLine("The Terminal.Gui interface requires a proper terminal environment.");
            Console.WriteLine("Press Ctrl+C to exit.");
            var cancellationToken = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cancellationToken.Cancel();
            };
            while (!cancellationToken.Token.IsCancellationRequested) Thread.Sleep(1000);
        }
        finally
        {
            try
            {
                Application.Shutdown();
            }
            catch
            {
            }
        }
    }

    private void CreateMainWindow()
    {
        _mainWindow = new Window
        {
            X = 0,
            Y = 0,
            Width = 80,
            Height = 25,
            Title = "║ Blackboard - System Console ║"
        };

        // System status panel with enhanced Borland styling
        var statusPanel = ThemeManager.CreateBorlandFrame("System Status", ThemeManager.ComponentStyles.StatusPrefix);
        statusPanel.X = 1;
        statusPanel.Y = 1;
        statusPanel.Width = 38;
        statusPanel.Height = 8;

        statusPanel.Add(ThemeManager.CreateBorlandLabel("System Status"));
        _statusLabel = ThemeManager.CreateBorlandLabel("Status: Offline", "🔴 ", 1, 1);
        _uptimeLabel = ThemeManager.CreateBorlandLabel("Uptime: 00:00:00", "⏰ ", 1, 2);
        var boardNameLabel = ThemeManager.CreateBorlandLabel($"Board: {_configManager.Configuration.System.BoardName}", "🏢 ", 1, 3);
        var sysopLabel = ThemeManager.CreateBorlandLabel($"Sysop: {_configManager.Configuration.System.SysopName}", "👨‍💼 ", 1, 4);
        var portLabel = ThemeManager.CreateBorlandLabel($"Port: {_configManager.Configuration.Network.TelnetPort}", "🔌 ", 1, 5);
        statusPanel.Add(_statusLabel, _uptimeLabel, boardNameLabel, sysopLabel, portLabel);

        // Server control panel with enhanced Borland styling
        var controlPanel = ThemeManager.CreateBorlandFrame("Server Control", ThemeManager.ComponentStyles.ServerPrefix);
        controlPanel.X = 41;
        controlPanel.Y = 1;
        controlPanel.Width = 38;
        controlPanel.Height = 8;

        controlPanel.Add(ThemeManager.CreateBorlandLabel("Server Control"));
        _startStopButton = ThemeManager.CreateBorlandButton("Start Server", "▶️ ");
        _startStopButton.X = 1;
        _startStopButton.Y = 1;
        _startStopButton.MouseClick += (sender, args) => OnStartStopClicked();

        var configButton = ThemeManager.CreateBorlandButton("Configuration", ThemeManager.ComponentStyles.ConfigPrefix);
        configButton.X = 1;
        configButton.Y = 3;
        configButton.MouseClick += (sender, args) => OnConfigurationClicked();

        var exitButton = ThemeManager.CreateBorlandButton("Exit", "🚪 ");
        exitButton.X = 1;
        exitButton.Y = 5;
        exitButton.MouseClick += (sender, args) => OnExitClicked();
        controlPanel.Add(_startStopButton, configButton, exitButton);

        // Active connections panel with enhanced Borland styling
        var connectionsPanel = ThemeManager.CreateBorlandFrame("Active Connections", ThemeManager.ComponentStyles.ConnectionPrefix);
        connectionsPanel.X = 1;
        connectionsPanel.Y = 10;
        connectionsPanel.Width = 78;
        connectionsPanel.Height = 13;

        connectionsPanel.Add(ThemeManager.CreateBorlandLabel("Active Connections"));
        _connectionsLabel = ThemeManager.CreateBorlandLabel("Connections: 0", "📊 ", 1, 1);
        _connectionsListView = new ListView { X = 1, Y = 2, Width = 60, Height = 10 };
        connectionsPanel.Add(_connectionsLabel, _connectionsListView);

        _mainWindow.Add(statusPanel, controlPanel, connectionsPanel);

        // Classic Borland-style menu bar with modern emojis
        var menu = new MenuBarv2(new[]
        {
            new MenuBarItemv2("System", new[]
            {
                new MenuItemv2("▶️ Start Server", "", () => OnStartStopClicked()),
                new MenuItemv2("⚙️ Configuration", "", () => OnConfigurationClicked()),
                null!,
                new MenuItemv2("🚪 Exit", "", () => OnExitClicked())
            }),
            new MenuBarItemv2("Admin", new[]
            {
                new MenuItemv2("🛡️ Dashboard", "", () => OnAdminDashboardClicked()),
                new MenuItemv2("👥 User Management", "", () => OnUserManagementClicked()),
                new MenuItemv2("📁 File Management", "", () => OnFileManagementClicked()),
                null!,
                new MenuItemv2("💾 Backup Database", "", () => OnDatabaseBackupClicked())
            }),
            new MenuBarItemv2("Tools", new[]
            {
                new MenuItemv2("🎨 Message Composer", "", () => OnAnsiEditorClicked()),
                new MenuItemv2("📝 Log Viewer", "", () => ShowNotImplemented("Log Viewer")),
                new MenuItemv2("📊 Statistics", "", () => ShowNotImplemented("Statistics"))
            }),
            new MenuBarItemv2("Help", new[]
            {
                new MenuItemv2("ℹ️ About Blackboard", "", () => OnAboutClicked()),
                new MenuItemv2("📖 Documentation", "", () => ShowNotImplemented("Documentation"))
            })
        });
        _mainWindow.Add(menu);
    }

    // --- ANSI Editor Integration ---
    private void OnAnsiEditorClicked()
    {
        try
        {
            var editor = new AnsiEditorWindow("ANSI Message Editor", composedText =>
            {
                MessageBox.Query("Message Saved", "Message composed and saved!\n\nPreview:\n" + composedText, "OK");
                // Here you would call the message service to save/send the message
            });
            Application.Run(editor);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error opening ANSI editor");
            MessageBox.ErrorQuery("Error", $"Failed to open ANSI editor: {ex.Message}", "OK");
        }
    }

    private void SetupEventHandlers()
    {
        _telnetServer.ClientConnected += OnClientConnected;
        _telnetServer.ClientDisconnected += OnClientDisconnected;
        _configManager.ConfigurationChanged += OnConfigurationChanged;
    }

    private void OnStartStopClicked()
    {
        Task.Run(async () =>
        {
            try
            {
                if (_telnetServer.IsRunning)
                {
                    await _telnetServer.StopAsync();
                    _logger.Information("Telnet server stopped by user");
                }
                else
                {
                    await _telnetServer.StartAsync();
                    _logger.Information("Telnet server started by user");
                }

                Application.Invoke(UpdateDisplay);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error toggling server state");
                Application.Invoke(() =>
                    MessageBox.ErrorQuery("Error", $"Failed to toggle server: {ex.Message}", "OK"));
            }
        });
    }

    private void OnAdminDashboardClicked()
    {
        try
        {
            var dashboard = new AdminDashboard(_statisticsService, _logger);
            Application.Run(dashboard);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error opening admin dashboard");
            MessageBox.ErrorQuery("Error", $"Failed to open admin dashboard: {ex.Message}", "OK");
        }
    }

    private void OnUserManagementClicked()
    {
        try
        {
            var userManagement = new UserManagementWindow(_userService, _auditService, _logger);
            Application.Run(userManagement);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error opening user management");
            MessageBox.ErrorQuery("Error", $"Failed to open user management: {ex.Message}", "OK");
        }
    }

    private void OnConfigurationClicked()
    {
        try
        {
            var configWindow = new ConfigurationWindow(_configManager, _logger);
            Application.Run(configWindow);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error opening configuration window");
            MessageBox.ErrorQuery("Error", $"Failed to open configuration: {ex.Message}", "OK");
        }
    }

    private void OnExitClicked()
    {
        var result = MessageBox.Query("Exit", "Are you sure you want to exit?", "Yes", "No");
        if (result == 0) Application.RequestStop();
    }

    private void OnDatabaseBackupClicked()
    {
        Task.Run(async () =>
        {
            try
            {
                var rootPath = _configManager.Configuration.System.RootPath;
                var backupDir = PathResolver.ResolvePath(
                    Core.Configuration.ConfigurationManager.DatabaseBackupPath,
                    rootPath);

                var backupFileName = $"blackboard-{DateTime.Now:yyyyMMdd-HHmmss}.db";
                var backupPath = Path.Combine(backupDir, backupFileName);

                await _databaseManager.BackupDatabaseAsync(backupPath);

                Application.Invoke(() =>
                    MessageBox.Query("Backup Complete", $"Database backed up to:\n{backupPath}", "OK"));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Database backup failed");
                Application.Invoke(() =>
                    MessageBox.ErrorQuery("Backup Failed", $"Database backup failed:\n{ex.Message}", "OK"));
            }
        });
    }

    private void OnAboutClicked()
    {
        MessageBox.Query("About Blackboard",
            "Blackboard v1.0\n\n" +
            "A modern BBS system with old school charm\n" +
            "Copyright (c) 2025", "OK");
    }

    private void ShowNotImplemented(string feature)
    {
        MessageBox.Query("Not Implemented", $"{feature} will be implemented in a future phase.", "OK");
    }

    private void OnClientConnected(object? sender, TelnetConnection connection)
    {
        _logger.Information("Client connected from {RemoteEndPoint}", connection.RemoteEndPointString);
        Application.Invoke(UpdateConnectionsList);
    }

    private void OnClientDisconnected(object? sender, TelnetConnection connection)
    {
        _logger.Information("Client disconnected from {RemoteEndPoint}", connection.RemoteEndPointString);
        Application.Invoke(UpdateConnectionsList);
    }

    private void OnConfigurationChanged(object? sender, SystemConfiguration config)
    {
        _logger.Information("Configuration changed, updating display and theme");

        // Apply the new theme if it changed
        ThemeManager.ApplyTheme(config.System.Theme);

        Application.Invoke(UpdateDisplay);
    }

    private bool UpdateTimerCallback()
    {
        Application.Invoke(UpdateDisplay);
        return true; // Continue timer
    }

    private void UpdateDisplay()
    {
        try
        {
            var isServerRunning = _telnetServer.IsRunning;

            if (_statusLabel != null)
            {
                var statusIcon = isServerRunning ? "🟢" : "🔴";
                var statusText = isServerRunning ? "Online" : "Offline";
                _statusLabel.Text = $"{statusIcon} Status: {statusText}";
            }

            if (_uptimeLabel != null && isServerRunning && _telnetServer.StartTime.HasValue)
            {
                // Calculate actual uptime since server start
                var uptime = DateTime.UtcNow - _telnetServer.StartTime.Value;
                _uptimeLabel.Text = $"⏰ Uptime: {uptime:hh\\:mm\\:ss}";
            }
            else if (_uptimeLabel != null)
            {
                _uptimeLabel.Text = "⏰ Uptime: 00:00:00";
            }

            if (_startStopButton != null)
            {
                var buttonIcon = isServerRunning ? "⏹️" : "▶️";
                var buttonText = isServerRunning ? "Stop Server" : "Start Server";
                _startStopButton.Text = $"{ThemeManager.ComponentStyles.ButtonLeft}{buttonIcon} {buttonText}{ThemeManager.ComponentStyles.ButtonRight}";
            }

            UpdateConnectionsList();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Exception in UpdateDisplay");
        }
    }

    private void UpdateConnectionsList()
    {
        try
        {
            if (_connectionsListView == null || _connectionsLabel == null)
                return;

            var connections = _telnetServer.ActiveConnections;
            _connectionsLabel.Text = $"📊 Connections: {connections.Count}";

            var connectionStrings = new ObservableCollection<string>(connections
                .Select(c => $"🌐 {c.RemoteEndPointString} - Connected: {c.ConnectedAt:HH:mm:ss}")
                .ToList());

            _connectionsListView.SetSource(connectionStrings);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Exception in UpdateConnectionsList");
        }
    }

    private void OnFileManagementClicked()
    {
        try
        {
            var fileManagement = new FileAreaManagementWindow(_fileAreaService, _logger);
            Application.Run(fileManagement);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error opening file management");
            MessageBox.ErrorQuery("Error", $"Failed to open file management: {ex.Message}", "OK");
        }
    }
}