using System.Collections.ObjectModel;
using Terminal.Gui.App;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Configuration;
using Serilog;
using Blackboard.Core.Configuration;
using Blackboard.Core.Network;
using Blackboard.Core.Services;
using Blackboard.Data;
using Blackboard.Data.Configuration;
using Blackboard.UI.Admin;

namespace Blackboard.UI;

public class MainApplication
{
    private readonly ILogger _logger;
    private readonly Blackboard.Core.Configuration.ConfigurationManager _configManager;
    private readonly DatabaseManager _databaseManager;
    private readonly TelnetServer _telnetServer;
    
    // Core services for admin functionality
    private readonly IUserService _userService;
    private readonly IAuditService _auditService;
    private readonly ISystemStatisticsService _statisticsService;
    private readonly IFileAreaService _fileAreaService;
    
    private Window? _mainWindow;
    private Label? _statusLabel;
    private Label? _uptimeLabel;
    private Label? _connectionsLabel;
    private ListView? _connectionsListView;
    private Button? _startStopButton;

    public MainApplication(ILogger logger, Blackboard.Core.Configuration.ConfigurationManager configManager, 
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
        _fileAreaService = new FileAreaService(databaseManager, logger);
    }

    public void Run()
    {
        try
        {
            Application.Init();
            
            // Enable Terminal.Gui's ConfigurationManager for theme support
            Terminal.Gui.Configuration.ConfigurationManager.Enable(Terminal.Gui.Configuration.ConfigLocations.All);
            
            // Apply the configured theme
            ThemeManager.ApplyTheme(_configManager.Configuration.System.Theme);
            
            CreateMainWindow();
            SetupEventHandlers();
            UpdateDisplay();
            Application.AddTimeout(TimeSpan.FromSeconds(1), () => { UpdateDisplay(); return true; });
            Application.Run(_mainWindow!);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Terminal UI failed to start, this may be due to running in a headless environment");
            Console.WriteLine("Blackboard is running in console mode.");
            Console.WriteLine("The Terminal.Gui interface requires a proper terminal environment.");
            Console.WriteLine("Press Ctrl+C to exit.");
            var cancellationToken = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) => {
                e.Cancel = true;
                cancellationToken.Cancel();
            };
            while (!cancellationToken.Token.IsCancellationRequested)
            {
                Thread.Sleep(1000);
            }
        }
        finally
        {
            try { Application.Shutdown(); } catch { }
        }
    }

    private void CreateMainWindow()
    {
        _mainWindow = new Window()
        {
            X = 0,
            Y = 0,
            Width = 80,
            Height = 25,
            Title = "Blackboard - System Console"
        };

        // System status panel
        var statusPanel = new FrameView()
        {
            X = 1,
            Y = 1,
            Width = 38,
            Height = 8
        };
        statusPanel.Add(new Label { X = 0, Y = 0, Text = "System Status" });
        _statusLabel = new Label { X = 1, Y = 1, Text = "Status: Offline" };
        _uptimeLabel = new Label { X = 1, Y = 2, Text = "Uptime: 00:00:00" };
        var boardNameLabel = new Label { X = 1, Y = 3, Text = $"Board: {_configManager.Configuration.System.BoardName}" };
        var sysopLabel = new Label { X = 1, Y = 4, Text = $"Sysop: {_configManager.Configuration.System.SysopName}" };
        var portLabel = new Label { X = 1, Y = 5, Text = $"Port: {_configManager.Configuration.Network.TelnetPort}" };
        statusPanel.Add(_statusLabel, _uptimeLabel, boardNameLabel, sysopLabel, portLabel);

        // Server control panel
        var controlPanel = new FrameView()
        {
            X = 41,
            Y = 1,
            Width = 38,
            Height = 8
        };
        controlPanel.Add(new Label { X = 0, Y = 0, Text = "Server Control" });
        _startStopButton = new Button { X = 1, Y = 1, Text = "Start Server" };
        _startStopButton.MouseClick += (sender, args) => OnStartStopClicked();
        var configButton = new Button { X = 1, Y = 3, Text = "Configuration" };
        configButton.MouseClick += (sender, args) => OnConfigurationClicked();
        var exitButton = new Button { X = 1, Y = 5, Text = "Exit" };
        exitButton.MouseClick += (sender, args) => OnExitClicked();
        controlPanel.Add(_startStopButton, configButton, exitButton);

        // Active connections panel
        var connectionsPanel = new FrameView()
        {
            X = 1,
            Y = 10,
            Width = 78,
            Height = 13
        };
        connectionsPanel.Add(new Label { X = 0, Y = 0, Text = "Active Connections" });
        _connectionsLabel = new Label { X = 1, Y = 1, Text = "Connections: 0" };
        _connectionsListView = new ListView { X = 1, Y = 2, Width = 60, Height = 10 };
        connectionsPanel.Add(_connectionsLabel, _connectionsListView);

        _mainWindow.Add(statusPanel, controlPanel, connectionsPanel);

        // Menu bar
        var menu = new MenuBarv2(new MenuBarItemv2[]
        {
            new MenuBarItemv2("_System", new MenuItemv2[]
            {
                new MenuItemv2("_Start Server", "", () => OnStartStopClicked()),
                new MenuItemv2("_Configuration", "", () => OnConfigurationClicked()),
                null!,
                new MenuItemv2("E_xit", "", () => OnExitClicked())
            }),
            new MenuBarItemv2("_Tools", new MenuItemv2[]
            {
                new MenuItemv2("_Admin Dashboard", "", () => OnAdminDashboardClicked()),
                new MenuItemv2("_User Management", "", () => OnUserManagementClicked()),
                new MenuItemv2("_File Management", "", () => OnFileManagementClicked()),
                new MenuItemv2("_Message Composer (ANSI)", "", () => OnAnsiEditorClicked()),
                new MenuItemv2("_Log Viewer", "", () => ShowNotImplemented("Log Viewer")),
                new MenuItemv2("_Database Backup", "", () => OnDatabaseBackupClicked())
            }),
            new MenuBarItemv2("_Help", new MenuItemv2[]
            {
                new MenuItemv2("_About", "", () => OnAboutClicked())
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
            Application.Run((Toplevel)editor);
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
                _logger.Error(ex, "Error toggling server state");                Application.Invoke(() =>
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
        if (result == 0)
        {
            Application.RequestStop();
        }
    }

    private void OnDatabaseBackupClicked()
    {
        Task.Run(async () =>
        {
            try
            {
                string rootPath = _configManager.Configuration.System.RootPath;
                string backupDir = Blackboard.Core.Configuration.PathResolver.ResolvePath(
                    _configManager.Configuration.Database.BackupPath, 
                    rootPath);
                
                string backupFileName = $"blackboard-{DateTime.Now:yyyyMMdd-HHmmss}.db";
                string backupPath = Path.Combine(backupDir, backupFileName);
                
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
        _logger.Information("Client connected from {RemoteEndPoint}", connection.RemoteEndPoint);
        Application.Invoke(UpdateConnectionsList);
    }

    private void OnClientDisconnected(object? sender, TelnetConnection connection)
    {
        _logger.Information("Client disconnected from {RemoteEndPoint}", connection.RemoteEndPoint);
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
                _statusLabel.Text = $"Status: {(isServerRunning ? "Online" : "Offline")}";
            }

            if (_uptimeLabel != null && isServerRunning && _telnetServer.StartTime.HasValue)
            {
                // Calculate actual uptime since server start
                var uptime = DateTime.UtcNow - _telnetServer.StartTime.Value;
                _uptimeLabel.Text = $"Uptime: {uptime:hh\\:mm\\:ss}";
            }
            else if (_uptimeLabel != null)
            {
                _uptimeLabel.Text = "Uptime: 00:00:00";
            }

            if (_startStopButton != null)
            {
                _startStopButton.Text = isServerRunning ? "Stop Server" : "Start Server";
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
            _connectionsLabel.Text = $"Connections: {connections.Count}";

            var connectionStrings = new ObservableCollection<string>(connections
                .Select(c => $"{c.RemoteEndPoint} - Connected: {c.ConnectedAt:HH:mm:ss}")
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
            Application.Run((Toplevel)fileManagement);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error opening file management");
            MessageBox.ErrorQuery("Error", $"Failed to open file management: {ex.Message}", "OK");
        }
    }
}
