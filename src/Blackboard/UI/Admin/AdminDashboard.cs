using System.Collections.ObjectModel;
using Terminal.Gui.App;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;
using Blackboard.Core.Services;
using Blackboard.Core.DTOs;
using Serilog;

namespace Blackboard.UI.Admin;

public class AdminDashboard : Window
{
    private readonly ISystemStatisticsService _statisticsService;
    private readonly ILogger _logger;
    
    private Label? _totalUsersLabel;
    private Label? _activeUsersLabel;
    private Label? _activeSessionsLabel;
    private Label? _uptimeLabel;
    private Label? _callsTodayLabel;
    private Label? _registrationsTodayLabel;
    private ListView? _activeSessionsList;
    private ListView? _alertsList;
    
    private Label? _memoryUsageLabel;
    private Label? _diskUsageLabel;
    private Label? _databaseStatusLabel;
    
    private Timer? _updateTimer;

    public AdminDashboard(ISystemStatisticsService statisticsService, ILogger logger)
    {
        _statisticsService = statisticsService;
        _logger = logger;
        
        Title = "Admin Dashboard";
        X = 0;
        Y = 0;
        Width = 80;
        Height = 25;
        
        InitializeComponent();
        SetupUpdateTimer();
    }

    private void InitializeComponent()
    {
        // Statistics Panel
        var statsFrame = new FrameView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Percent(50),
            Height = 10
        };
        statsFrame.Add(new Label { X = 0, Y = 0, Text = "System Statistics" });

        _totalUsersLabel = new Label { X = 1, Y = 1, Text = "Total Users: 0" };
        _activeUsersLabel = new Label { X = 1, Y = 2, Text = "Active Users: 0" };
        _activeSessionsLabel = new Label { X = 1, Y = 3, Text = "Active Sessions: 0" };
        _uptimeLabel = new Label { X = 1, Y = 4, Text = "System Uptime: 0s" };
        _callsTodayLabel = new Label { X = 1, Y = 5, Text = "Calls Today: 0" };
        _registrationsTodayLabel = new Label { X = 1, Y = 6, Text = "Registrations Today: 0" };

        statsFrame.Add(_totalUsersLabel, _activeUsersLabel, _activeSessionsLabel, 
                      _uptimeLabel, _callsTodayLabel, _registrationsTodayLabel);

        // System Resources Panel
        var resourcesFrame = new FrameView()
        {
            X = Pos.Right(statsFrame),
            Y = 0,
            Width = Dim.Fill(),
            Height = 10
        };
        resourcesFrame.Add(new Label { X = 0, Y = 0, Text = "System Resources" });

        _memoryUsageLabel = new Label { X = 1, Y = 1, Text = "Memory Usage: 0%" };
        _diskUsageLabel = new Label { X = 1, Y = 2, Text = "Disk Usage: 0%" };
        _databaseStatusLabel = new Label { X = 1, Y = 3, Text = "Database: Unknown" };

        resourcesFrame.Add(_memoryUsageLabel, _diskUsageLabel, _databaseStatusLabel);

        // Active Sessions Panel
        var sessionsFrame = new FrameView()
        {
            X = 0,
            Y = Pos.Bottom(statsFrame),
            Width = Dim.Percent(50),
            Height = 12
        };
        sessionsFrame.Add(new Label { X = 0, Y = 0, Text = "Active Sessions" });

        _activeSessionsList = new ListView
        {
            X = 1,
            Y = 1,
            Width = 36,
            Height = 10
        };

        sessionsFrame.Add(_activeSessionsList);

        // System Alerts Panel
        var alertsFrame = new FrameView()
        {
            X = Pos.Right(sessionsFrame),
            Y = Pos.Bottom(resourcesFrame),
            Width = 40,
            Height = 12
        };
        alertsFrame.Add(new Label { X = 0, Y = 0, Text = "System Alerts" });

        _alertsList = new ListView
        {
            X = 1,
            Y = 1,
            Width = 36,
            Height = 10
        };

        alertsFrame.Add(_alertsList);

        Add(statsFrame, resourcesFrame, sessionsFrame, alertsFrame);
    }

    private void SetupUpdateTimer()
    {
        // Update dashboard every 5 seconds
        _updateTimer = new Timer(async _ => await UpdateDashboard(), null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }

    private async void RefreshData()
    {
        await UpdateDashboard();
    }

    private async Task UpdateDashboard()
    {
        try
        {
            var dashboardData = await _statisticsService.GetDashboardStatisticsAsync();
            
            // Update UI on main thread - simplified for now
            UpdateStatistics(dashboardData.SystemStats);
            UpdateResourceInfo(dashboardData.SystemResources);
            UpdateDatabaseStatus(dashboardData.DatabaseStatus);
            UpdateActiveSessions(dashboardData.ActiveSessions);
            UpdateAlerts(dashboardData.SystemAlerts);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error updating admin dashboard");
        }
    }

    private void UpdateStatistics(SystemStatisticsDto stats)
    {
        if (_totalUsersLabel != null)
            _totalUsersLabel.Text = $"Total Users: {stats.TotalUsers}";
        
        if (_activeUsersLabel != null)
            _activeUsersLabel.Text = $"Active Users: {stats.ActiveUsers}";
        
        if (_activeSessionsLabel != null)
            _activeSessionsLabel.Text = $"Active Sessions: {stats.ActiveSessions}";
        
        if (_uptimeLabel != null)
            _uptimeLabel.Text = $"System Uptime: {FormatTimeSpan(stats.SystemUptime)}";
        
        if (_callsTodayLabel != null)
            _callsTodayLabel.Text = $"Calls Today: {stats.CallsToday}";
        
        if (_registrationsTodayLabel != null)
            _registrationsTodayLabel.Text = $"Registrations Today: {stats.RegistrationsToday}";
    }

    private void UpdateResourceInfo(SystemResourcesDto resources)
    {
        if (_memoryUsageLabel != null)
            _memoryUsageLabel.Text = $"Memory Usage: {resources.MemoryUsagePercent:F1}% ({FormatBytes(resources.MemoryUsedBytes)})";
        
        if (_diskUsageLabel != null)
            _diskUsageLabel.Text = $"Disk Usage: {resources.DiskUsagePercent:F1}% ({FormatBytes(resources.DiskUsedBytes)})";
    }

    private void UpdateDatabaseStatus(DatabaseStatusDto dbStatus)
    {
        if (_databaseStatusLabel != null)
        {
            var status = dbStatus.IsConnected ? "Connected" : "Disconnected";
            var walStatus = dbStatus.WalModeEnabled ? " (WAL)" : "";
            _databaseStatusLabel.Text = $"Database: {status}{walStatus} - {FormatBytes(dbStatus.DatabaseSizeBytes)}";
        }
    }

    private void UpdateActiveSessions(IEnumerable<ActiveSessionDto> sessions)
    {
        if (_activeSessionsList != null)
        {
            var sessionItems = sessions.Select(s => 
                $"{s.Handle,-15} {s.IpAddress,-15} {FormatTimeSpan(s.SessionDuration),-10} {s.CurrentActivity}"
            ).ToList();
            
            if (sessionItems.Count == 0)
            {
                sessionItems.Add("No active sessions");
            }
            
            var observableItems = new ObservableCollection<string>(sessionItems);
            _activeSessionsList.SetSource<string>(observableItems);
        }
    }

    private void UpdateAlerts(IEnumerable<SystemAlertDto> alerts)
    {
        if (_alertsList != null)
        {
            var alertItems = alerts.Take(10).Select(a => 
                $"[{a.Severity}] {a.Title}: {a.Message}"
            ).ToList();
            
            if (alertItems.Count == 0)
            {
                alertItems.Add("No active alerts");
            }
            
            var observableItems = new ObservableCollection<string>(alertItems);
            _alertsList.SetSource<string>(observableItems);
        }
    }

    private string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalDays >= 1)
            return $"{(int)timeSpan.TotalDays}d {timeSpan.Hours}h {timeSpan.Minutes}m";
        else if (timeSpan.TotalHours >= 1)
            return $"{timeSpan.Hours}h {timeSpan.Minutes}m";
        else
            return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
    }

    private string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _updateTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}
