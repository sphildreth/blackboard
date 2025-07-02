using Terminal.Gui;
using Serilog;
using Blackboard.Core.Configuration;
using Blackboard.Core.Network;
using Blackboard.Data;

namespace Blackboard.UI;

public class MainApplication
{
    private readonly ILogger _logger;
    private readonly ConfigurationManager _configManager;
    private readonly DatabaseManager _databaseManager;
    private readonly TelnetServer _telnetServer;
    
    private Window? _mainWindow;
    private Label? _statusLabel;
    private Label? _uptimeLabel;
    private Label? _connectionsLabel;
    private ListView? _connectionsListView;
    private Button? _startStopButton;
    private bool _isServerRunning;

    public MainApplication(ILogger logger, ConfigurationManager configManager, 
        DatabaseManager databaseManager, TelnetServer telnetServer)
    {
        _logger = logger;
        _configManager = configManager;
        _databaseManager = databaseManager;
        _telnetServer = telnetServer;
    }

    public void Run()
    {
        try
        {
            Application.Init();
            CreateMainWindow();
            SetupEventHandlers();
            UpdateDisplay();
            Application.MainLoop.AddTimeout(TimeSpan.FromSeconds(1), _ => { UpdateDisplay(); return true; });
            Application.Run(_mainWindow);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Terminal UI failed to start, this may be due to running in a headless environment");
            Console.WriteLine("Blackboard BBS is running in console mode.");
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
        _mainWindow = new Window("Blackboard BBS - System Console")
        {
            X = 0,
            Y = 0,
            Width = 80,
            Height = 25
        };

        // System status panel
        var statusPanel = new View()
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
        var controlPanel = new View()
        {
            X = 41,
            Y = 1,
            Width = 38,
            Height = 8
        };
        controlPanel.Add(new Label { X = 0, Y = 0, Text = "Server Control" });
        _startStopButton = new Button { X = 1, Y = 1, Text = "Start Server" };
        _startStopButton.Clicked += OnStartStopClicked;
        var configButton = new Button { X = 1, Y = 3, Text = "Configuration" };
        configButton.Clicked += OnConfigurationClicked;
        var exitButton = new Button { X = 1, Y = 5, Text = "Exit" };
        exitButton.Clicked += OnExitClicked;
        controlPanel.Add(_startStopButton, configButton, exitButton);

        // Active connections panel
        var connectionsPanel = new View()
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
        var menu = new MenuBar(new MenuBarItem[]
        {
            new MenuBarItem("_System", new MenuItem[]
            {
                new MenuItem("_Start Server", "", OnStartStopClicked),
                new MenuItem("_Configuration", "", OnConfigurationClicked),
                null!,
                new MenuItem("E_xit", "", OnExitClicked)
            }),
            new MenuBarItem("_Tools", new MenuItem[]
            {
                new MenuItem("_User Management", "", () => ShowNotImplemented("User Management")),
                new MenuItem("_Log Viewer", "", () => ShowNotImplemented("Log Viewer")),
                new MenuItem("_Database Backup", "", OnDatabaseBackupClicked)
            }),
            new MenuBarItem("_Help", new MenuItem[]
            {
                new MenuItem("_About", "", OnAboutClicked)
            })
        });
        _mainWindow.Add(menu);
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
                if (_isServerRunning)
                {
                    await _telnetServer.StopAsync();
                    _isServerRunning = false;
                    _logger.Information("Telnet server stopped by user");
                }
                else
                {
                    await _telnetServer.StartAsync();
                    _isServerRunning = true;
                    _logger.Information("Telnet server started by user");
                }

                Application.MainLoop.Invoke(UpdateDisplay);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error toggling server state");
                Application.MainLoop.Invoke(() => 
                    MessageBox.ErrorQuery("Error", $"Failed to toggle server: {ex.Message}", "OK"));
            }
        });
    }

    private void OnConfigurationClicked()
    {
        ShowNotImplemented("Configuration Editor");
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
                var backupPath = Path.Combine("backups", $"blackboard-{DateTime.Now:yyyyMMdd-HHmmss}.db");
                await _databaseManager.BackupDatabaseAsync(backupPath);
                
                Application.MainLoop.Invoke(() =>
                    MessageBox.Query("Backup Complete", $"Database backed up to:\n{backupPath}", "OK"));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Database backup failed");
                Application.MainLoop.Invoke(() =>
                    MessageBox.ErrorQuery("Backup Failed", $"Database backup failed:\n{ex.Message}", "OK"));
            }
        });
    }

    private void OnAboutClicked()
    {
        MessageBox.Query("About Blackboard BBS", 
            "Blackboard BBS v1.0\n\n" +
            "A modern terminal-based BBS application\n" +
            "Built with .NET 8 and Terminal.Gui\n\n" +
            "Copyright (c) 2025", "OK");
    }

    private void ShowNotImplemented(string feature)
    {
        MessageBox.Query("Not Implemented", $"{feature} will be implemented in a future phase.", "OK");
    }

    private void OnClientConnected(object? sender, TelnetConnection connection)
    {
        _logger.Information("Client connected from {RemoteEndPoint}", connection.RemoteEndPoint);
        Application.MainLoop.Invoke(UpdateConnectionsList);
    }

    private void OnClientDisconnected(object? sender, TelnetConnection connection)
    {
        _logger.Information("Client disconnected from {RemoteEndPoint}", connection.RemoteEndPoint);
        Application.MainLoop.Invoke(UpdateConnectionsList);
    }

    private void OnConfigurationChanged(object? sender, SystemConfiguration config)
    {
        _logger.Information("Configuration changed, updating display");
        Application.MainLoop.Invoke(UpdateDisplay);
    }

    private bool UpdateTimerCallback()
    {
        Application.MainLoop.Invoke(UpdateDisplay);
        return true; // Continue timer
    }

    private void UpdateDisplay()
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text = $"Status: {(_isServerRunning ? "Online" : "Offline")}";
        }

        if (_uptimeLabel != null && _isServerRunning)
        {
            // Calculate uptime (simplified)
            _uptimeLabel.Text = $"Uptime: {DateTime.UtcNow.TimeOfDay:hh\\:mm\\:ss}";
        }

        if (_startStopButton != null)
        {
            _startStopButton.Text = _isServerRunning ? "Stop Server" : "Start Server";
        }

        UpdateConnectionsList();
    }

    private void UpdateConnectionsList()
    {
        if (_connectionsListView == null || _connectionsLabel == null)
            return;

        var connections = _telnetServer.ActiveConnections;
        _connectionsLabel.Text = $"Connections: {connections.Count}";

        var connectionStrings = connections
            .Select(c => $"{c.RemoteEndPoint} - Connected: {c.ConnectedAt:HH:mm:ss}")
            .ToList();

        _connectionsListView.SetSource(connectionStrings);
    }
}
