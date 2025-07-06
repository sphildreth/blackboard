using System.Collections.ObjectModel;
using Blackboard.Core.DTOs;
using Blackboard.Core.Services;
using Serilog;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Blackboard.UI.Admin;

public class AdminDashboard : Window
{
    private readonly ILogger _logger;
    private readonly ISystemStatisticsService _statisticsService;
    private Label? _activeSessionsLabel;
    private ListView? _activeSessionsList;
    private Label? _activeUsersLabel;
    private ListView? _alertsList;
    private Label? _callsTodayLabel;
    private Label? _databaseStatusLabel;
    private Label? _diskUsageLabel;

    private Label? _memoryUsageLabel;
    private Label? _registrationsTodayLabel;

    private Label? _totalUsersLabel;

    private Timer? _updateTimer;
    private Label? _uptimeLabel;

    public AdminDashboard(ISystemStatisticsService statisticsService, ILogger logger)
    {
        _statisticsService = statisticsService;
        _logger = logger;

        Title = "║ Admin Dashboard ║";
        X = 0;
        Y = 0;
        Width = 80;
        Height = 25;

        InitializeComponent();
        SetupUpdateTimer();
    }

    private void InitializeComponent()
    {
        // Statistics Panel with enhanced Borland styling
        var statsFrame = ThemeManager.CreateBorlandFrame("System Statistics", ThemeManager.ComponentStyles.StatisticsPrefix);
        statsFrame.X = 0;
        statsFrame.Y = 0;
        statsFrame.Width = Dim.Percent(50);
        statsFrame.Height = 10;
        statsFrame.Add(ThemeManager.CreateBorlandLabel("System Statistics"));

        _totalUsersLabel = ThemeManager.CreateBorlandLabel("Total Users: 0", ThemeManager.ComponentStyles.UserPrefix, 1, 1);
        _activeUsersLabel = ThemeManager.CreateBorlandLabel("Active Users: 0", "👥 ", 1, 2);
        _activeSessionsLabel = ThemeManager.CreateBorlandLabel("Active Sessions: 0", "🔗 ", 1, 3);
        _uptimeLabel = ThemeManager.CreateBorlandLabel("System Uptime: 0s", "⏰ ", 1, 4);
        _callsTodayLabel = ThemeManager.CreateBorlandLabel("Calls Today: 0", "📞 ", 1, 5);
        _registrationsTodayLabel = ThemeManager.CreateBorlandLabel("Registrations Today: 0", "📝 ", 1, 6);

        statsFrame.Add(_totalUsersLabel, _activeUsersLabel, _activeSessionsLabel,
            _uptimeLabel, _callsTodayLabel, _registrationsTodayLabel);

        // System Resources Panel with enhanced styling
        var resourcesFrame = ThemeManager.CreateBorlandFrame("System Resources", ThemeManager.ComponentStyles.ResourcePrefix);
        resourcesFrame.X = Pos.Right(statsFrame);
        resourcesFrame.Y = 0;
        resourcesFrame.Width = Dim.Fill();
        resourcesFrame.Height = 10;
        resourcesFrame.Add(ThemeManager.CreateBorlandLabel("System Resources"));

        _memoryUsageLabel = ThemeManager.CreateBorlandLabel("Memory Usage: 0%", "🧠 ", 1, 1);
        _diskUsageLabel = ThemeManager.CreateBorlandLabel("Disk Usage: 0%", "💾 ", 1, 2);
        _databaseStatusLabel = ThemeManager.CreateBorlandLabel("Database: Unknown", "🗄️ ", 1, 3);

        resourcesFrame.Add(_memoryUsageLabel, _diskUsageLabel, _databaseStatusLabel);

        // Active Sessions Panel with enhanced styling
        var sessionsFrame = ThemeManager.CreateBorlandFrame("Active Sessions", "🔗 ");
        sessionsFrame.X = 0;
        sessionsFrame.Y = Pos.Bottom(statsFrame);
        sessionsFrame.Width = Dim.Percent(50);
        sessionsFrame.Height = 12;

        sessionsFrame.Add(ThemeManager.CreateBorlandLabel("Active Sessions"));

        _activeSessionsList = new ListView
        {
            X = 1,
            Y = 1,
            Width = 36,
            Height = 10
        };

        sessionsFrame.Add(_activeSessionsList);

        // System Alerts Panel with enhanced styling
        var alertsFrame = ThemeManager.CreateBorlandFrame("System Alerts", ThemeManager.ComponentStyles.AlertPrefix);
        alertsFrame.X = Pos.Right(sessionsFrame);
        alertsFrame.Y = Pos.Bottom(resourcesFrame);
        alertsFrame.Width = 40;
        alertsFrame.Height = 12;

        alertsFrame.Add(ThemeManager.CreateBorlandLabel("System Alerts"));

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
            _totalUsersLabel.Text = $"👤 Total Users: {stats.TotalUsers}";

        if (_activeUsersLabel != null)
            _activeUsersLabel.Text = $"👥 Active Users: {stats.ActiveUsers}";

        if (_activeSessionsLabel != null)
            _activeSessionsLabel.Text = $"🔗 Active Sessions: {stats.ActiveSessions}";

        if (_uptimeLabel != null)
            _uptimeLabel.Text = $"⏰ System Uptime: {FormatTimeSpan(stats.SystemUptime)}";

        if (_callsTodayLabel != null)
            _callsTodayLabel.Text = $"📞 Calls Today: {stats.CallsToday}";

        if (_registrationsTodayLabel != null)
            _registrationsTodayLabel.Text = $"📝 Registrations Today: {stats.RegistrationsToday}";
    }

    private void UpdateResourceInfo(SystemResourcesDto resources)
    {
        var memoryIcon = resources.MemoryUsagePercent > 80 ? "🔴" : resources.MemoryUsagePercent > 60 ? "🟡" : "🟢";
        var diskIcon = resources.DiskUsagePercent > 80 ? "🔴" : resources.DiskUsagePercent > 60 ? "🟡" : "🟢";

        if (_memoryUsageLabel != null)
            _memoryUsageLabel.Text = $"🧠 Memory Usage: {memoryIcon} {resources.MemoryUsagePercent:F1}% ({FormatBytes(resources.MemoryUsedBytes)})";

        if (_diskUsageLabel != null)
            _diskUsageLabel.Text = $"💾 Disk Usage: {diskIcon} {resources.DiskUsagePercent:F1}% ({FormatBytes(resources.DiskUsedBytes)})";
    }

    private void UpdateDatabaseStatus(DatabaseStatusDto dbStatus)
    {
        if (_databaseStatusLabel != null)
        {
            var statusIcon = dbStatus.IsConnected ? "🟢" : "🔴";
            var status = dbStatus.IsConnected ? "Connected" : "Disconnected";
            var walStatus = dbStatus.WalModeEnabled ? " (WAL)" : "";
            _databaseStatusLabel.Text = $"🗄️ Database: {statusIcon} {status}{walStatus} - {FormatBytes(dbStatus.DatabaseSizeBytes)}";
        }
    }

    private void UpdateActiveSessions(IEnumerable<ActiveSessionDto> sessions)
    {
        if (_activeSessionsList != null)
        {
            var sessionItems = sessions.Select(s =>
                $"🔗 {s.Handle,-15} {s.IpAddress,-15} {FormatTimeSpan(s.SessionDuration),-10} {s.CurrentActivity}"
            ).ToList();

            if (sessionItems.Count == 0) sessionItems.Add("📭 No active sessions");

            var observableItems = new ObservableCollection<string>(sessionItems);
            _activeSessionsList.SetSource(observableItems);
        }
    }

    private void UpdateAlerts(IEnumerable<SystemAlertDto> alerts)
    {
        if (_alertsList != null)
        {
            var alertItems = alerts.Take(10).Select(a =>
            {
                var icon = a.Severity switch
                {
                    AlertSeverity.Error => "🔴",
                    AlertSeverity.Critical => "🔴",
                    AlertSeverity.Warning => "🟡",
                    AlertSeverity.Info => "🔵",
                    _ => "⚠️"
                };
                return $"{icon} [{a.Severity}] {a.Title}: {a.Message}";
            }).ToList();

            if (alertItems.Count == 0) alertItems.Add("No active alerts");

            var observableItems = new ObservableCollection<string>(alertItems);
            _alertsList.SetSource(observableItems);
        }
    }

    private string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalDays >= 1)
            return $"{(int)timeSpan.TotalDays}d {timeSpan.Hours}h {timeSpan.Minutes}m";
        if (timeSpan.TotalHours >= 1)
            return $"{timeSpan.Hours}h {timeSpan.Minutes}m";
        return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
    }

    private string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        var order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _updateTimer?.Dispose();
        base.Dispose(disposing);
    }
}