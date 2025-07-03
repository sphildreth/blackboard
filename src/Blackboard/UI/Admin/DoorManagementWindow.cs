using System.Text;
using Blackboard.Core.DTOs;
using Blackboard.Core.Services;
using Terminal.Gui;

namespace Blackboard.UI.Admin;

/// <summary>
/// Admin interface for Door Game System management
/// </summary>
public class DoorManagementWindow : Window
{
    private readonly IDoorService _doorService;
    private readonly IFossilEmulationService _fossilService;
    private ListView _doorsList;
    private ListView _sessionsList;
    private Label _statsLabel;
    private Button _addDoorButton;
    private Button _editDoorButton;
    private Button _deleteDoorButton;
    private Button _testDoorButton;
    private Button _viewLogsButton;
    private Button _maintenanceButton;
    private Button _refreshButton;

    private List<DoorDto> _doors = new();
    private List<DoorSessionDto> _sessions = new();

    public DoorManagementWindow(IDoorService doorService, IFossilEmulationService fossilService)
        : base("Door Game System Management")
    {
        _doorService = doorService ?? throw new ArgumentNullException(nameof(doorService));
        _fossilService = fossilService ?? throw new ArgumentNullException(nameof(fossilService));

        InitializeComponents();
        SetupLayout();
        SetupEventHandlers();
        
        _ = RefreshDataAsync();
    }

    private void InitializeComponents()
    {
        // Door list
        _doorsList = new ListView
        {
            X = 0,
            Y = 1,
            Width = Dim.Percent(50),
            Height = Dim.Percent(60),
            AllowsMarking = false,
            CanFocus = true
        };

        // Active sessions list
        _sessionsList = new ListView
        {
            X = Pos.Right(_doorsList) + 1,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Percent(60),
            AllowsMarking = false,
            CanFocus = true
        };

        // Statistics label
        _statsLabel = new Label
        {
            X = 0,
            Y = Pos.Bottom(_doorsList) + 1,
            Width = Dim.Fill(),
            Height = 3,
            Text = "Loading statistics..."
        };

        // Buttons
        var buttonY = Pos.Bottom(_statsLabel) + 1;
        _addDoorButton = new Button("Add Door") { X = 0, Y = buttonY };
        _editDoorButton = new Button("Edit Door") { X = Pos.Right(_addDoorButton) + 1, Y = buttonY };
        _deleteDoorButton = new Button("Delete Door") { X = Pos.Right(_editDoorButton) + 1, Y = buttonY };
        _testDoorButton = new Button("Test Door") { X = Pos.Right(_deleteDoorButton) + 1, Y = buttonY };
        
        buttonY = Pos.Bottom(_addDoorButton) + 1;
        _viewLogsButton = new Button("View Logs") { X = 0, Y = buttonY };
        _maintenanceButton = new Button("Maintenance") { X = Pos.Right(_viewLogsButton) + 1, Y = buttonY };
        _refreshButton = new Button("Refresh") { X = Pos.Right(_maintenanceButton) + 1, Y = buttonY };
    }

    private void SetupLayout()
    {
        // Add labels
        Add(new Label("Doors:") { X = 0, Y = 0 });
        Add(new Label("Active Sessions:") { X = Pos.Right(_doorsList) + 1, Y = 0 });

        // Add components
        Add(_doorsList);
        Add(_sessionsList);
        Add(_statsLabel);
        Add(_addDoorButton);
        Add(_editDoorButton);
        Add(_deleteDoorButton);
        Add(_testDoorButton);
        Add(_viewLogsButton);
        Add(_maintenanceButton);
        Add(_refreshButton);
    }

    private void SetupEventHandlers()
    {
        _addDoorButton.Clicked += OnAddDoor;
        _editDoorButton.Clicked += OnEditDoor;
        _deleteDoorButton.Clicked += OnDeleteDoor;
        _testDoorButton.Clicked += OnTestDoor;
        _viewLogsButton.Clicked += OnViewLogs;
        _maintenanceButton.Clicked += OnMaintenance;
        _refreshButton.Clicked += OnRefresh;

        _doorsList.SelectedItemChanged += OnDoorSelectionChanged;
    }

    private async Task RefreshDataAsync()
    {
        try
        {
            // Load doors
            var doors = await _doorService.GetAllDoorsAsync();
            _doors = doors.ToList();

            // Load active sessions
            var sessions = await _doorService.GetRecentSessionsAsync(50);
            _sessions = sessions.ToList();

            // Load statistics
            var stats = await _doorService.GetDoorSystemStatisticsAsync();

            Application.MainLoop.Invoke(() =>
            {
                UpdateDoorsDisplay();
                UpdateSessionsDisplay();
                UpdateStatisticsDisplay(stats);
            });
        }
        catch (Exception ex)
        {
            Application.MainLoop.Invoke(() =>
            {
                MessageBox.ErrorQuery("Error", $"Failed to refresh data: {ex.Message}", "OK");
            });
        }
    }

    private void UpdateDoorsDisplay()
    {
        var items = _doors.Select(d => 
            $"{d.Name} ({d.Category}) - {(d.IsActive ? "Active" : "Inactive")} - {d.ActiveSessions} sessions"
        ).ToList();

        _doorsList.SetSource(items);
    }

    private void UpdateSessionsDisplay()
    {
        var items = _sessions.Select(s =>
            $"{s.DoorName} - {s.UserHandle} - {s.Status} - {s.StartTime:HH:mm:ss}"
        ).ToList();

        _sessionsList.SetSource(items);
    }

    private void UpdateStatisticsDisplay(DoorSystemStatisticsDto stats)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Total Doors: {stats.TotalDoors} | Active: {stats.ActiveDoors}");
        sb.AppendLine($"Active Sessions: {stats.ActiveSessions} | Total Sessions: {stats.TotalSessions}");
        sb.AppendLine($"Total Session Time: {TimeSpan.FromSeconds(stats.TotalSessionTime)} | Unique Players Today: {stats.UniquePlayersToday}");

        _statsLabel.Text = sb.ToString();
    }

    private void OnAddDoor()
    {
        var dialog = new DoorEditDialog(null, _doorService);
        Application.Run(dialog);
        
        if (dialog.Saved)
        {
            _ = RefreshDataAsync();
        }
    }

    private void OnEditDoor()
    {
        if (_doorsList.SelectedItem < 0 || _doorsList.SelectedItem >= _doors.Count)
        {
            MessageBox.ErrorQuery("Error", "Please select a door to edit.", "OK");
            return;
        }

        var selectedDoor = _doors[_doorsList.SelectedItem];
        var dialog = new DoorEditDialog(selectedDoor, _doorService);
        Application.Run(dialog);
        
        if (dialog.Saved)
        {
            _ = RefreshDataAsync();
        }
    }

    private async void OnDeleteDoor()
    {
        if (_doorsList.SelectedItem < 0 || _doorsList.SelectedItem >= _doors.Count)
        {
            MessageBox.ErrorQuery("Error", "Please select a door to delete.", "OK");
            return;
        }

        var selectedDoor = _doors[_doorsList.SelectedItem];
        var result = MessageBox.Query("Confirm Delete", 
            $"Are you sure you want to delete '{selectedDoor.Name}'?", "Yes", "No");

        if (result == 0) // Yes
        {
            try
            {
                await _doorService.DeleteDoorAsync(selectedDoor.Id);
                await RefreshDataAsync();
                MessageBox.Query("Success", "Door deleted successfully.", "OK");
            }
            catch (Exception ex)
            {
                MessageBox.ErrorQuery("Error", $"Failed to delete door: {ex.Message}", "OK");
            }
        }
    }

    private async void OnTestDoor()
    {
        if (_doorsList.SelectedItem < 0 || _doorsList.SelectedItem >= _doors.Count)
        {
            MessageBox.ErrorQuery("Error", "Please select a door to test.", "OK");
            return;
        }

        var selectedDoor = _doors[_doorsList.SelectedItem];
        
        try
        {
            var issues = await _doorService.ValidateDoorConfigurationAsync(selectedDoor.Id);
            
            if (!issues.Any())
            {
                MessageBox.Query("Test Results", "Door configuration is valid!", "OK");
            }
            else
            {
                var issuesList = string.Join("\n", issues);
                MessageBox.ErrorQuery("Test Results", $"Issues found:\n{issuesList}", "OK");
            }
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", $"Failed to test door: {ex.Message}", "OK");
        }
    }

    private void OnViewLogs()
    {
        if (_doorsList.SelectedItem < 0 || _doorsList.SelectedItem >= _doors.Count)
        {
            MessageBox.ErrorQuery("Error", "Please select a door to view logs.", "OK");
            return;
        }

        var selectedDoor = _doors[_doorsList.SelectedItem];
        var logsDialog = new DoorLogsDialog(selectedDoor.Id, _doorService);
        Application.Run(logsDialog);
    }

    private void OnMaintenance()
    {
        var maintenanceDialog = new DoorMaintenanceDialog(_doorService);
        Application.Run(maintenanceDialog);
        
        if (maintenanceDialog.RefreshNeeded)
        {
            _ = RefreshDataAsync();
        }
    }

    private void OnRefresh()
    {
        _ = RefreshDataAsync();
    }

    private void OnDoorSelectionChanged(ListViewItemEventArgs args)
    {
        bool hasSelection = args.Item >= 0 && args.Item < _doors.Count;
        _editDoorButton.Enabled = hasSelection;
        _deleteDoorButton.Enabled = hasSelection;
        _testDoorButton.Enabled = hasSelection;
        _viewLogsButton.Enabled = hasSelection;
    }
}

/// <summary>
/// Dialog for adding/editing doors
/// </summary>
public class DoorEditDialog : Dialog
{
    private readonly DoorDto? _door;
    private readonly IDoorService _doorService;
    
    private TextField _nameField;
    private TextField _descriptionField;
    private TextField _categoryField;
    private TextField _executablePathField;
    private TextField _commandLineField;
    private TextField _workingDirectoryField;
    private TextField _dropFileTypeField;
    private CheckBox _isActiveCheckBox;
    private CheckBox _requiresDosBoxCheckBox;
    private TextField _minimumLevelField;
    private TextField _timeLimitField;
    private TextField _dailyLimitField;

    public bool Saved { get; private set; }

    public DoorEditDialog(DoorDto? door, IDoorService doorService) 
        : base(door == null ? "Add Door" : "Edit Door", 80, 25)
    {
        _door = door;
        _doorService = doorService;
        
        InitializeComponents();
        LoadData();
        SetupButtons();
    }

    private void InitializeComponents()
    {
        var y = 1;
        
        Add(new Label("Name:") { X = 1, Y = y });
        _nameField = new TextField { X = 20, Y = y, Width = 40 };
        Add(_nameField);
        y += 2;

        Add(new Label("Description:") { X = 1, Y = y });
        _descriptionField = new TextField { X = 20, Y = y, Width = 40 };
        Add(_descriptionField);
        y += 2;

        Add(new Label("Category:") { X = 1, Y = y });
        _categoryField = new TextField { X = 20, Y = y, Width = 40 };
        Add(_categoryField);
        y += 2;

        Add(new Label("Executable Path:") { X = 1, Y = y });
        _executablePathField = new TextField { X = 20, Y = y, Width = 40 };
        Add(_executablePathField);
        y += 2;

        Add(new Label("Command Line:") { X = 1, Y = y });
        _commandLineField = new TextField { X = 20, Y = y, Width = 40 };
        Add(_commandLineField);
        y += 2;

        Add(new Label("Working Directory:") { X = 1, Y = y });
        _workingDirectoryField = new TextField { X = 20, Y = y, Width = 40 };
        Add(_workingDirectoryField);
        y += 2;

        Add(new Label("Drop File Type:") { X = 1, Y = y });
        _dropFileTypeField = new TextField { X = 20, Y = y, Width = 40 };
        Add(_dropFileTypeField);
        y += 2;

        _isActiveCheckBox = new CheckBox("Is Active") { X = 1, Y = y };
        Add(_isActiveCheckBox);
        y += 2;

        _requiresDosBoxCheckBox = new CheckBox("Requires DOSBox") { X = 1, Y = y };
        Add(_requiresDosBoxCheckBox);
        y += 2;

        Add(new Label("Minimum Level:") { X = 1, Y = y });
        _minimumLevelField = new TextField { X = 20, Y = y, Width = 10 };
        Add(_minimumLevelField);
        y += 2;

        Add(new Label("Time Limit (min):") { X = 1, Y = y });
        _timeLimitField = new TextField { X = 20, Y = y, Width = 10 };
        Add(_timeLimitField);
        y += 2;

        Add(new Label("Daily Limit:") { X = 1, Y = y });
        _dailyLimitField = new TextField { X = 20, Y = y, Width = 10 };
        Add(_dailyLimitField);
    }

    private void LoadData()
    {
        if (_door != null)
        {
            _nameField.Text = _door.Name;
            _descriptionField.Text = _door.Description ?? "";
            _categoryField.Text = _door.Category;
            _executablePathField.Text = _door.ExecutablePath;
            _commandLineField.Text = _door.CommandLine ?? "";
            _workingDirectoryField.Text = _door.WorkingDirectory ?? "";
            _dropFileTypeField.Text = _door.DropFileType;
            _isActiveCheckBox.Checked = _door.IsActive;
            _requiresDosBoxCheckBox.Checked = _door.RequiresDosBox;
            _minimumLevelField.Text = _door.MinimumLevel.ToString();
            _timeLimitField.Text = _door.TimeLimit.ToString();
            _dailyLimitField.Text = _door.DailyLimit.ToString();
        }
        else
        {
            _dropFileTypeField.Text = "DOOR.SYS";
            _isActiveCheckBox.Checked = true;
            _minimumLevelField.Text = "0";
            _timeLimitField.Text = "60";
            _dailyLimitField.Text = "5";
        }
    }

    private void SetupButtons()
    {
        var saveButton = new Button("Save");
        var cancelButton = new Button("Cancel");
        
        AddButton(saveButton);
        AddButton(cancelButton);
        
        saveButton.Clicked += OnSave;
        cancelButton.Clicked += OnCancel;
    }

    private async void OnSave()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_nameField.Text.ToString()))
            {
                MessageBox.ErrorQuery("Error", "Name is required.", "OK");
                return;
            }

            if (string.IsNullOrWhiteSpace(_executablePathField.Text.ToString()))
            {
                MessageBox.ErrorQuery("Error", "Executable path is required.", "OK");
                return;
            }

            if (_door == null)
            {
                // Create new door
                var createDto = new CreateDoorDto
                {
                    Name = _nameField.Text.ToString()!,
                    Description = _descriptionField.Text.ToString(),
                    Category = _categoryField.Text.ToString() ?? "General",
                    ExecutablePath = _executablePathField.Text.ToString()!,
                    CommandLine = _commandLineField.Text.ToString(),
                    WorkingDirectory = _workingDirectoryField.Text.ToString(),
                    DropFileType = _dropFileTypeField.Text.ToString() ?? "DOOR.SYS",
                    RequiresDosBox = _requiresDosBoxCheckBox.Checked,
                    MinimumLevel = int.TryParse(_minimumLevelField.Text.ToString(), out var minLevel) ? minLevel : 0,
                    TimeLimit = int.TryParse(_timeLimitField.Text.ToString(), out var timeLimit) ? timeLimit : 60,
                    DailyLimit = int.TryParse(_dailyLimitField.Text.ToString(), out var dailyLimit) ? dailyLimit : 5
                };

                await _doorService.CreateDoorAsync(createDto, 1); // TODO: Get actual admin user ID
            }
            else
            {
                // Update existing door
                _door.Name = _nameField.Text.ToString()!;
                _door.Description = _descriptionField.Text.ToString();
                _door.Category = _categoryField.Text.ToString() ?? "General";
                _door.ExecutablePath = _executablePathField.Text.ToString()!;
                _door.CommandLine = _commandLineField.Text.ToString();
                _door.WorkingDirectory = _workingDirectoryField.Text.ToString();
                _door.DropFileType = _dropFileTypeField.Text.ToString() ?? "DOOR.SYS";
                _door.IsActive = _isActiveCheckBox.Checked;
                _door.RequiresDosBox = _requiresDosBoxCheckBox.Checked;
                _door.MinimumLevel = int.TryParse(_minimumLevelField.Text.ToString(), out var minLevel) ? minLevel : 0;
                _door.TimeLimit = int.TryParse(_timeLimitField.Text.ToString(), out var timeLimit) ? timeLimit : 60;
                _door.DailyLimit = int.TryParse(_dailyLimitField.Text.ToString(), out var dailyLimit) ? dailyLimit : 5;

                await _doorService.UpdateDoorAsync(_door);
            }

            Saved = true;
            Application.RequestStop();
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", $"Failed to save door: {ex.Message}", "OK");
        }
    }

    private void OnCancel()
    {
        Application.RequestStop();
    }
}

/// <summary>
/// Dialog for viewing door logs
/// </summary>
public class DoorLogsDialog : Dialog
{
    private readonly int _doorId;
    private readonly IDoorService _doorService;
    private ListView _logsList;

    public DoorLogsDialog(int doorId, IDoorService doorService) : base("Door Logs", 100, 30)
    {
        _doorId = doorId;
        _doorService = doorService;
        
        InitializeComponents();
        _ = LoadLogsAsync();
    }

    private void InitializeComponents()
    {
        _logsList = new ListView
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(1),
            Height = Dim.Fill(2),
            AllowsMarking = false
        };

        Add(_logsList);
        
        var closeButton = new Button("Close");
        AddButton(closeButton);
        closeButton.Clicked += () => Application.RequestStop();
    }

    private async Task LoadLogsAsync()
    {
        try
        {
            var logs = await _doorService.GetDoorLogsAsync(_doorId, count: 100);
            var items = logs.Select(log =>
                $"{log.Timestamp:yyyy-MM-dd HH:mm:ss} [{log.LogLevel.ToUpper()}] {log.Message}"
            ).ToList();

            Application.MainLoop.Invoke(() =>
            {
                _logsList.SetSource(items);
            });
        }
        catch (Exception ex)
        {
            Application.MainLoop.Invoke(() =>
            {
                MessageBox.ErrorQuery("Error", $"Failed to load logs: {ex.Message}", "OK");
            });
        }
    }
}

/// <summary>
/// Dialog for door maintenance operations
/// </summary>
public class DoorMaintenanceDialog : Dialog
{
    private readonly IDoorService _doorService;
    private Button _cleanupSessionsButton;
    private Button _cleanupFilesButton;
    private Button _validateAllButton;
    private Label _statusLabel;

    public bool RefreshNeeded { get; private set; }

    public DoorMaintenanceDialog(IDoorService doorService) : base("Door Maintenance", 60, 15)
    {
        _doorService = doorService;
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        var y = 1;

        _cleanupSessionsButton = new Button("Cleanup Expired Sessions") { X = 1, Y = y };
        Add(_cleanupSessionsButton);
        y += 3;

        _cleanupFilesButton = new Button("Cleanup Orphaned Files") { X = 1, Y = y };
        Add(_cleanupFilesButton);
        y += 3;

        _validateAllButton = new Button("Validate All Doors") { X = 1, Y = y };
        Add(_validateAllButton);
        y += 3;

        _statusLabel = new Label("Ready") { X = 1, Y = y, Width = Dim.Fill(1) };
        Add(_statusLabel);

        var closeButton = new Button("Close");
        AddButton(closeButton);

        _cleanupSessionsButton.Clicked += OnCleanupSessions;
        _cleanupFilesButton.Clicked += OnCleanupFiles;
        _validateAllButton.Clicked += OnValidateAll;
        closeButton.Clicked += () => Application.RequestStop();
    }

    private async void OnCleanupSessions()
    {
        try
        {
            _statusLabel.Text = "Cleaning up expired sessions...";
            var count = await _doorService.CleanupExpiredSessionsAsync();
            _statusLabel.Text = $"Cleaned up {count} expired sessions.";
            RefreshNeeded = true;
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Error: {ex.Message}";
        }
    }

    private async void OnCleanupFiles()
    {
        try
        {
            _statusLabel.Text = "Cleaning up orphaned files...";
            var count = await _doorService.CleanupOrphanedFilesAsync();
            _statusLabel.Text = $"Cleaned up {count} orphaned files.";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Error: {ex.Message}";
        }
    }

    private async void OnValidateAll()
    {
        try
        {
            _statusLabel.Text = "Validating all doors...";
            var doors = await _doorService.GetAllDoorsAsync();
            var totalIssues = 0;

            foreach (var door in doors)
            {
                var issues = await _doorService.ValidateDoorConfigurationAsync(door.Id);
                totalIssues += issues.Count();
            }

            _statusLabel.Text = totalIssues == 0 
                ? "All doors validated successfully!" 
                : $"Found {totalIssues} total issues across all doors.";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Error: {ex.Message}";
        }
    }
}
