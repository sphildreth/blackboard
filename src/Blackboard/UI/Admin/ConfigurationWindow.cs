using Blackboard.Core.Configuration;
using Serilog;
using Terminal.Gui.App;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Blackboard.UI.Admin;

public class ConfigurationWindow : Window
{
    private readonly ConfigurationManager _configManager;
    private readonly ILogger _logger;
    private TextField? _bindAddressField;
    private TextField? _boardNameField;
    private TextField? _connectionTimeoutField;
    private TextField? _locationField;
    private TextField? _lockoutDurationField;
    private TextField? _maxConnectionsField;

    private TextField? _maxLoginAttemptsField;
    private TextField? _maxUsersField;
    private TextField? _passwordExpirationField;
    private TextField? _passwordMinLengthField;
    private TextField? _preEnterCodeField;
    private CheckBox? _requirePasswordComplexityCheck;
    private CheckBox? _requirePreEnterCodeCheck;

    private Label? _statusLabel;
    private TextField? _sysopNameField;
    private CheckBox? _systemOnlineCheck;

    private TabView? _tabView;
    // Note: Theme field removed - using custom enhanced visual theme

    private TextField? _telnetPortField;

    public ConfigurationWindow(ConfigurationManager configManager, ILogger logger)
    {
        _configManager = configManager;
        _logger = logger;

        Title = "║ System Configuration ║";
        X = 0;
        Y = 0;
        Width = 80;
        Height = 25;

        InitializeComponent();
        LoadConfiguration();
    }

    private void InitializeComponent()
    {
        _tabView = new TabView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(3)
        };

        // System Settings Tab
        var systemTab = new Tab
        {
            Text = "System",
            View = CreateSystemSettingsView()
        };

        // Network Settings Tab  
        var networkTab = new Tab
        {
            Text = "Network",
            View = CreateNetworkSettingsView()
        };

        // Security Settings Tab
        var securityTab = new Tab
        {
            Text = "Security",
            View = CreateSecuritySettingsView()
        };

        _tabView.AddTab(systemTab, false);
        _tabView.AddTab(networkTab, false);
        _tabView.AddTab(securityTab, false);

        // Action buttons with enhanced styling
        var saveButton = ThemeManager.CreateBorlandButton("Save Configuration", "💾 ");
        saveButton.X = 2;
        saveButton.Y = Pos.Bottom(_tabView) + 1;
        saveButton.MouseClick += (s, e) => SaveConfiguration();

        var reloadButton = ThemeManager.CreateBorlandButton("Reload", "🔄 ");
        reloadButton.X = 22;
        reloadButton.Y = Pos.Bottom(_tabView) + 1;
        reloadButton.MouseClick += (s, e) => LoadConfiguration();

        var closeButton = ThemeManager.CreateBorlandButton("Close", "❌ ");
        closeButton.X = 32;
        closeButton.Y = Pos.Bottom(_tabView) + 1;
        closeButton.MouseClick += (s, e) => Application.RequestStop();

        // Status label
        _statusLabel = new Label
        {
            X = 45,
            Y = Pos.Bottom(_tabView) + 1,
            Width = Dim.Fill(),
            Text = "📋 Ready"
        };

        Add(_tabView, saveButton, reloadButton, closeButton, _statusLabel);
    }

    private View CreateSystemSettingsView()
    {
        var view = new View
        {
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        // Board Name
        view.Add(new Label { X = 2, Y = 1, Text = "Board Name:" });
        _boardNameField = new TextField
        {
            X = 20,
            Y = 1,
            Width = 30
        };
        view.Add(_boardNameField);

        // Sysop Name
        view.Add(new Label { X = 2, Y = 3, Text = "Sysop Name:" });
        _sysopNameField = new TextField
        {
            X = 20,
            Y = 3,
            Width = 30
        };
        view.Add(_sysopNameField);

        // Location
        view.Add(new Label { X = 2, Y = 5, Text = "Location:" });
        _locationField = new TextField
        {
            X = 20,
            Y = 5,
            Width = 30
        };
        view.Add(_locationField);

        // Max Users
        view.Add(new Label { X = 2, Y = 7, Text = "Max Users:" });
        _maxUsersField = new TextField
        {
            X = 20,
            Y = 7,
            Width = 10
        };
        view.Add(_maxUsersField);

        // System Online
        _systemOnlineCheck = new CheckBox
        {
            X = 2,
            Y = 9,
            Text = "System Online"
        };
        view.Add(_systemOnlineCheck);

        // Require Pre-enter Code
        _requirePreEnterCodeCheck = new CheckBox
        {
            X = 2,
            Y = 11,
            Text = "Require Pre-enter Code"
        };
        view.Add(_requirePreEnterCodeCheck);

        // Pre-enter Code
        view.Add(new Label { X = 2, Y = 13, Text = "Pre-enter Code:" });
        _preEnterCodeField = new TextField
        {
            X = 20,
            Y = 13,
            Width = 20
        };
        view.Add(_preEnterCodeField);

        // Visual Enhancement Info (Borland-style theme)
        view.Add(new Label { X = 2, Y = 15, Text = "UI Style:" });
        var themeInfoLabel = new Label
        {
            X = 20,
            Y = 15,
            Width = 40,
            Height = 2,
            Text = "🎨 Classic Borland Theme\n(Retro 1990s IDE + Modern Icons)"
        };
        view.Add(themeInfoLabel);

        return view;
    }

    private View CreateNetworkSettingsView()
    {
        var view = new View
        {
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        // Telnet Port
        view.Add(new Label { X = 2, Y = 1, Text = "Telnet Port:" });
        _telnetPortField = new TextField
        {
            X = 20,
            Y = 1,
            Width = 10
        };
        view.Add(_telnetPortField);

        // Bind Address
        view.Add(new Label { X = 2, Y = 3, Text = "Bind Address:" });
        _bindAddressField = new TextField
        {
            X = 20,
            Y = 3,
            Width = 20
        };
        view.Add(_bindAddressField);

        // Max Connections
        view.Add(new Label { X = 2, Y = 5, Text = "Max Connections:" });
        _maxConnectionsField = new TextField
        {
            X = 20,
            Y = 5,
            Width = 10
        };
        view.Add(_maxConnectionsField);

        // Connection Timeout
        view.Add(new Label { X = 2, Y = 7, Text = "Connection Timeout:" });
        _connectionTimeoutField = new TextField
        {
            X = 20,
            Y = 7,
            Width = 10
        };
        view.Add(_connectionTimeoutField);
        view.Add(new Label { X = 31, Y = 7, Text = "seconds" });

        return view;
    }

    private View CreateSecuritySettingsView()
    {
        var view = new View
        {
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        // Max Login Attempts
        view.Add(new Label { X = 2, Y = 1, Text = "Max Login Attempts:" });
        _maxLoginAttemptsField = new TextField
        {
            X = 25,
            Y = 1,
            Width = 10
        };
        view.Add(_maxLoginAttemptsField);

        // Lockout Duration
        view.Add(new Label { X = 2, Y = 3, Text = "Lockout Duration:" });
        _lockoutDurationField = new TextField
        {
            X = 25,
            Y = 3,
            Width = 10
        };
        view.Add(_lockoutDurationField);
        view.Add(new Label { X = 36, Y = 3, Text = "minutes" });

        // Password Min Length
        view.Add(new Label { X = 2, Y = 5, Text = "Password Min Length:" });
        _passwordMinLengthField = new TextField
        {
            X = 25,
            Y = 5,
            Width = 10
        };
        view.Add(_passwordMinLengthField);

        // Require Password Complexity
        _requirePasswordComplexityCheck = new CheckBox
        {
            X = 2,
            Y = 7,
            Text = "Require Password Complexity"
        };
        view.Add(_requirePasswordComplexityCheck);

        // Password Expiration
        view.Add(new Label { X = 2, Y = 9, Text = "Password Expiration:" });
        _passwordExpirationField = new TextField
        {
            X = 25,
            Y = 9,
            Width = 10
        };
        view.Add(_passwordExpirationField);
        view.Add(new Label { X = 36, Y = 9, Text = "days" });

        return view;
    }

    private void LoadConfiguration()
    {
        try
        {
            var config = _configManager.Configuration;

            // System settings
            if (_boardNameField != null) _boardNameField.Text = config.System.BoardName;
            if (_sysopNameField != null) _sysopNameField.Text = config.System.SysopName;
            if (_locationField != null) _locationField.Text = config.System.Location;
            if (_maxUsersField != null) _maxUsersField.Text = config.System.MaxUsers.ToString();
            if (_systemOnlineCheck != null) _systemOnlineCheck.CheckedState = config.System.SystemOnline ? CheckState.Checked : CheckState.UnChecked;
            if (_requirePreEnterCodeCheck != null) _requirePreEnterCodeCheck.CheckedState = config.System.RequirePreEnterCode ? CheckState.Checked : CheckState.UnChecked;
            if (_preEnterCodeField != null) _preEnterCodeField.Text = config.System.PreEnterCode;
            // Note: Theme is now fixed to "Enhanced" - no user configuration needed

            // Network settings
            if (_telnetPortField != null) _telnetPortField.Text = config.Network.TelnetPort.ToString();
            if (_bindAddressField != null) _bindAddressField.Text = config.Network.TelnetBindAddress;
            if (_maxConnectionsField != null) _maxConnectionsField.Text = config.Network.MaxConcurrentConnections.ToString();
            if (_connectionTimeoutField != null) _connectionTimeoutField.Text = config.Network.ConnectionTimeoutSeconds.ToString();

            // Security settings
            if (_maxLoginAttemptsField != null) _maxLoginAttemptsField.Text = config.Security.MaxLoginAttempts.ToString();
            if (_lockoutDurationField != null) _lockoutDurationField.Text = config.Security.LockoutDurationMinutes.ToString();
            if (_passwordMinLengthField != null) _passwordMinLengthField.Text = config.Security.PasswordMinLength.ToString();
            if (_requirePasswordComplexityCheck != null) _requirePasswordComplexityCheck.CheckedState = config.Security.RequirePasswordComplexity ? CheckState.Checked : CheckState.UnChecked;
            if (_passwordExpirationField != null) _passwordExpirationField.Text = config.Security.PasswordExpirationDays.ToString();

            if (_statusLabel != null) _statusLabel.Text = "Configuration loaded successfully";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading configuration");
            if (_statusLabel != null) _statusLabel.Text = "Error loading configuration";
        }
    }

    private void SaveConfiguration()
    {
        try
        {
            if (_statusLabel != null) _statusLabel.Text = "Saving configuration...";

            var config = _configManager.Configuration;

            // Update system settings
            config.System.BoardName = _boardNameField?.Text ?? config.System.BoardName;
            config.System.SysopName = _sysopNameField?.Text ?? config.System.SysopName;
            config.System.Location = _locationField?.Text ?? config.System.Location;

            if (int.TryParse(_maxUsersField?.Text, out var maxUsers))
                config.System.MaxUsers = maxUsers;

            config.System.SystemOnline = _systemOnlineCheck?.CheckedState == CheckState.Checked;
            config.System.RequirePreEnterCode = _requirePreEnterCodeCheck?.CheckedState == CheckState.Checked;
            config.System.PreEnterCode = _preEnterCodeField?.Text ?? config.System.PreEnterCode;
            // Note: Theme is now fixed to "Enhanced" for custom visual styling

            // Update network settings
            if (int.TryParse(_telnetPortField?.Text, out var telnetPort))
                config.Network.TelnetPort = telnetPort;

            config.Network.TelnetBindAddress = _bindAddressField?.Text ?? config.Network.TelnetBindAddress;

            if (int.TryParse(_maxConnectionsField?.Text, out var maxConnections))
                config.Network.MaxConcurrentConnections = maxConnections;

            if (int.TryParse(_connectionTimeoutField?.Text, out var connectionTimeout))
                config.Network.ConnectionTimeoutSeconds = connectionTimeout;

            // Update security settings
            if (int.TryParse(_maxLoginAttemptsField?.Text, out var maxLoginAttempts))
                config.Security.MaxLoginAttempts = maxLoginAttempts;

            if (int.TryParse(_lockoutDurationField?.Text, out var lockoutDuration))
                config.Security.LockoutDurationMinutes = lockoutDuration;

            if (int.TryParse(_passwordMinLengthField?.Text, out var passwordMinLength))
                config.Security.PasswordMinLength = passwordMinLength;

            config.Security.RequirePasswordComplexity = _requirePasswordComplexityCheck?.CheckedState == CheckState.Checked;

            if (int.TryParse(_passwordExpirationField?.Text, out var passwordExpiration))
                config.Security.PasswordExpirationDays = passwordExpiration;

            // Save the configuration
            _configManager.SaveConfiguration();

            // Apply the new theme immediately
            ThemeManager.ApplyTheme(config.System.Theme);

            if (_statusLabel != null) _statusLabel.Text = "Configuration saved successfully";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error saving configuration");
            if (_statusLabel != null) _statusLabel.Text = "Error saving configuration";
            MessageBox.ErrorQuery("Error", $"Error saving configuration: {ex.Message}", "OK");
        }
    }
}