using System.Collections.ObjectModel;
using System.Text;
using Blackboard.Core.DTOs;
using Blackboard.Core.Services;
using Terminal.Gui.App;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;

namespace Blackboard.UI.Admin;

/// <summary>
/// Admin interface for Door Game System management
/// Updated for Terminal.Gui v2 alpha compatibility
/// </summary>
public class DoorManagementWindow : Window
{
    private readonly IDoorService _doorService;
    private readonly IFossilEmulationService _fossilService;
    private ListView? _doorsList;
    private ListView? _sessionsList;
    private Label? _statsLabel;
    private Button? _addDoorButton;
    private Button? _editDoorButton;
    private Button? _deleteDoorButton;
    private Button? _testDoorButton;
    private Button? _viewLogsButton;
    private Button? _maintenanceButton;
    private Button? _refreshButton;

    private List<DoorDto> _doors = new();
    private List<DoorSessionDto> _sessions = new();

    public DoorManagementWindow(IDoorService doorService, IFossilEmulationService fossilService)
        : base()
    {
        _doorService = doorService ?? throw new ArgumentNullException(nameof(doorService));
        _fossilService = fossilService ?? throw new ArgumentNullException(nameof(fossilService));
        
        Title = "Door Game System Management";
        X = 0;
        Y = 0;
        Width = 120;
        Height = 35;

        InitializeComponents();
        SetupLayout();
        SetupEventHandlers();
        
        _ = RefreshDataAsync();
    }

    private void InitializeComponents()
    {
        // Door list
        _doorsList = new ListView()
        {
            X = 0,
            Y = 1,
            Width = 60,
            Height = 20
        };

        // Session list
        _sessionsList = new ListView()
        {
            X = 62,
            Y = 1,
            Width = 56,
            Height = 20
        };

        // Statistics label
        _statsLabel = new Label()
        {
            X = 0,
            Y = 22,
            Width = 118,
            Height = 3,
            Text = "Loading statistics..."
        };

        // Buttons row 1
        var buttonY = 26;
        _addDoorButton = new Button()
        {
            X = 0,
            Y = buttonY,
            Width = 12,
            Height = 1,
            Text = "Add Door"
        };
        
        _editDoorButton = new Button()
        {
            X = 13,
            Y = buttonY,
            Width = 12,
            Height = 1,
            Text = "Edit Door"
        };
        
        _deleteDoorButton = new Button()
        {
            X = 26,
            Y = buttonY,
            Width = 14,
            Height = 1,
            Text = "Delete Door"
        };
        
        _testDoorButton = new Button()
        {
            X = 41,
            Y = buttonY,
            Width = 12,
            Height = 1,
            Text = "Test Door"
        };

        // Buttons row 2
        buttonY = 28;
        _viewLogsButton = new Button()
        {
            X = 0,
            Y = buttonY,
            Width = 12,
            Height = 1,
            Text = "View Logs"
        };
        
        _maintenanceButton = new Button()
        {
            X = 13,
            Y = buttonY,
            Width = 14,
            Height = 1,
            Text = "Maintenance"
        };
        
        _refreshButton = new Button()
        {
            X = 28,
            Y = buttonY,
            Width = 10,
            Height = 1,
            Text = "Refresh"
        };
    }

    private void SetupLayout()
    {
        Add(new Label() { X = 0, Y = 0, Text = "Doors:" });
        Add(new Label() { X = 62, Y = 0, Text = "Active Sessions:" });
        Add(_doorsList!);
        Add(_sessionsList!);
        Add(_statsLabel!);
        Add(_addDoorButton!);
        Add(_editDoorButton!);
        Add(_deleteDoorButton!);
        Add(_testDoorButton!);
        Add(_viewLogsButton!);
        Add(_maintenanceButton!);
        Add(_refreshButton!);
    }

    private void SetupEventHandlers()
    {
        _doorsList!.SelectedItemChanged += (sender, args) => OnDoorSelected(args);
        
        // For Terminal.Gui v2, buttons use MouseClick event
        _addDoorButton!.MouseClick += (sender, args) => OnAddDoor();
        _editDoorButton!.MouseClick += (sender, args) => OnEditDoor();
        _deleteDoorButton!.MouseClick += (sender, args) => OnDeleteDoor();
        _testDoorButton!.MouseClick += (sender, args) => OnTestDoor();
        _viewLogsButton!.MouseClick += (sender, args) => OnViewLogs();
        _maintenanceButton!.MouseClick += (sender, args) => OnMaintenance();
        _refreshButton!.MouseClick += (sender, args) => OnRefresh();
    }

    private async Task RefreshDataAsync()
    {
        try
        {
            // Load doors
            _doors = (await _doorService.GetAllDoorsAsync()).ToList();
            var doorItems = _doors.Select(d => $"{d.Name} - {d.Category} ({(d.IsActive ? "Active" : "Inactive")})").ToArray();
            
            // Load active sessions
            _sessions = (await _doorService.GetActiveSessionsAsync()).ToList();
            var sessionItems = _sessions.Select(s => $"{s.UserHandle} - {s.DoorName} ({s.Duration}s)").ToArray();

            // Update statistics
            var stats = await _doorService.GetDoorSystemStatisticsAsync();
            var statsText = $"Total Doors: {stats.TotalDoors} | Active: {stats.ActiveDoors} | Sessions Today: {stats.TotalSessionsToday}";

            // Update UI on main thread - use ObservableCollection for Terminal.Gui v2
            _doorsList!.SetSource(new ObservableCollection<string>(doorItems));
            _sessionsList!.SetSource(new ObservableCollection<string>(sessionItems));
            _statsLabel!.Text = statsText;
        }
        catch (Exception ex)
        {
            _statsLabel!.Text = $"Error loading data: {ex.Message}";
        }
    }

    #region Event Handlers

    private void OnDoorSelected(EventArgs args)
    {
        var selectedIndex = _doorsList!.SelectedItem;
        if (selectedIndex >= 0 && selectedIndex < _doors.Count)
        {
            var selectedDoor = _doors[selectedIndex];
            _ = LoadDoorSessionsAsync(selectedDoor.Id);
        }
    }

    private async Task LoadDoorSessionsAsync(int doorId)
    {
        try
        {
            var doorSessions = (await _doorService.GetActiveSessionsForDoorAsync(doorId)).ToList();
            var sessionItems = doorSessions.Select(s => $"{s.UserHandle} - Started: {s.StartTime:HH:mm} Duration: {s.Duration}s").ToArray();
            
            _sessionsList!.SetSource(new ObservableCollection<string>(sessionItems));
        }
        catch (Exception ex)
        {
            _sessionsList!.SetSource(new ObservableCollection<string>(new[] { $"Error: {ex.Message}" }));
        }
    }

    private void OnAddDoor()
    {
        _ = ShowAddDoorDialogAsync();
    }

    private async Task ShowAddDoorDialogAsync()
    {
        try
        {
            // Simplified add door - in a real implementation, you'd create a proper dialog
            var result = MessageBox.Query("Add Door", "Door creation dialog not fully implemented for Terminal.Gui v2.\nWould you like to create a test door?", "Yes", "No");
            if (result == 0)
            {
                var createDto = new CreateDoorDto
                {
                    Name = "Test Door",
                    Category = "Test",
                    ExecutablePath = "/bin/echo",
                    CommandLine = "Hello from test door!",
                    MinimumLevel = 0,
                    TimeLimit = 60,
                    DailyLimit = 5
                };

                await _doorService.CreateDoorAsync(createDto, 1); // TODO: Get actual admin user ID
                await RefreshDataAsync();
                MessageBox.Query("Success", "Test door created successfully!", "OK");
            }
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", $"Failed to create door: {ex.Message}", "OK");
        }
    }

    private void OnEditDoor()
    {
        var selectedIndex = _doorsList!.SelectedItem;
        if (selectedIndex >= 0 && selectedIndex < _doors.Count)
        {
            var door = _doors[selectedIndex];
            MessageBox.Query("Edit Door", $"Edit functionality for {door.Name} not yet fully implemented for Terminal.Gui v2", "OK");
        }
        else
        {
            MessageBox.ErrorQuery("Error", "Please select a door to edit", "OK");
        }
    }

    private void OnDeleteDoor()
    {
        var selectedIndex = _doorsList!.SelectedItem;
        if (selectedIndex >= 0 && selectedIndex < _doors.Count)
        {
            var door = _doors[selectedIndex];
            _ = DeleteDoorAsync(door);
        }
        else
        {
            MessageBox.ErrorQuery("Error", "Please select a door to delete", "OK");
        }
    }

    private async Task DeleteDoorAsync(DoorDto door)
    {
        var result = MessageBox.Query("Confirm Delete", $"Delete door '{door.Name}'?", "Yes", "No");
        if (result == 0)
        {
            try
            {
                await _doorService.DeleteDoorAsync(door.Id);
                await RefreshDataAsync();
                MessageBox.Query("Success", "Door deleted successfully!", "OK");
            }
            catch (Exception ex)
            {
                MessageBox.ErrorQuery("Error", $"Failed to delete door: {ex.Message}", "OK");
            }
        }
    }

    private void OnTestDoor()
    {
        var selectedIndex = _doorsList!.SelectedItem;
        if (selectedIndex >= 0 && selectedIndex < _doors.Count)
        {
            var door = _doors[selectedIndex];
            _ = TestDoorAsync(door);
        }
        else
        {
            MessageBox.ErrorQuery("Error", "Please select a door to test", "OK");
        }
    }

    private async Task TestDoorAsync(DoorDto door)
    {
        try
        {
            var result = await _doorService.TestDoorExecutableAsync(door.Id);
            var message = result ? "Door test passed!" : "Door test failed!";
            MessageBox.Query("Test Result", message, "OK");
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", $"Test failed: {ex.Message}", "OK");
        }
    }

    private void OnViewLogs()
    {
        var selectedIndex = _doorsList!.SelectedItem;
        if (selectedIndex >= 0 && selectedIndex < _doors.Count)
        {
            var door = _doors[selectedIndex];
            _ = ShowLogsAsync(door.Id);
        }
        else
        {
            MessageBox.ErrorQuery("Error", "Please select a door to view logs", "OK");
        }
    }

    private async Task ShowLogsAsync(int doorId)
    {
        try
        {
            var logs = await _doorService.GetDoorLogsAsync(doorId, count: 20);
            var logMessages = logs.Select(l => $"[{l.Timestamp:HH:mm:ss}] {l.LogLevel}: {l.Message}").ToArray();
            var logText = string.Join("\n", logMessages);
            
            MessageBox.Query("Door Logs", $"Recent logs:\n{logText}", "OK");
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", $"Failed to load logs: {ex.Message}", "OK");
        }
    }

    private void OnMaintenance()
    {
        _ = ShowMaintenanceAsync();
    }

    private async Task ShowMaintenanceAsync()
    {
        var result = MessageBox.Query("Maintenance", "Select maintenance operation:", "Cleanup Sessions", "Cleanup Files", "Validate All", "Cancel");
        
        try
        {
            switch (result)
            {
                case 0: // Cleanup Sessions
                    var sessionCount = await _doorService.CleanupExpiredSessionsAsync();
                    MessageBox.Query("Maintenance", $"Cleaned up {sessionCount} expired sessions", "OK");
                    break;
                    
                case 1: // Cleanup Files
                    var fileCount = await _doorService.CleanupOrphanedFilesAsync();
                    MessageBox.Query("Maintenance", $"Cleaned up {fileCount} orphaned files", "OK");
                    break;
                    
                case 2: // Validate All
                    var doors = await _doorService.GetAllDoorsAsync();
                    var totalIssues = 0;
                    foreach (var door in doors)
                    {
                        var issues = await _doorService.ValidateDoorConfigurationAsync(door.Id);
                        totalIssues += issues.Count();
                    }
                    var validationMessage = totalIssues == 0 ? "All doors validated successfully!" : $"Found {totalIssues} issues across all doors";
                    MessageBox.Query("Validation", validationMessage, "OK");
                    break;
            }
            
            if (result < 3) // If not Cancel
            {
                await RefreshDataAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", $"Maintenance operation failed: {ex.Message}", "OK");
        }
    }

    private void OnRefresh()
    {
        _ = RefreshDataAsync();
    }

    #endregion
}
