using System.Collections.ObjectModel;
using Terminal.Gui.App;
using Terminal.Gui.Views;
using Terminal.Gui.ViewBase;
using Blackboard.Core.Services;
using Blackboard.Core.DTOs;
using Blackboard.Core.Models;
using Serilog;

namespace Blackboard.UI.Admin;

public class UserManagementWindow : Window
{
    private readonly IUserService _userService;
    private readonly IAuditService _auditService;
    private readonly ILogger _logger;
    
    private ListView? _usersList;
    private TextField? _searchField;
    private Label? _statusLabel;
    private Button? _editButton;
    private Button? _lockButton;
    private Button? _unlockButton;
    private Button? _deleteButton;
    private Button? _refreshButton;
    
    private List<UserProfileDto> _users = new();
    private UserProfileDto? _selectedUser;

    public UserManagementWindow(IUserService userService, IAuditService auditService, ILogger logger)
    {
        _userService = userService;
        _auditService = auditService;
        _logger = logger;
        
        Title = "User Management";
        X = 0;
        Y = 0;
        Width = 80;
        Height = 25;
        
        InitializeComponent();
        LoadUsers();
    }

    private void InitializeComponent()
    {
        // Enhanced search panel
        var searchFrame = ThemeManager.CreateStyledFrame("Search Users", "🔍 ");
        searchFrame.X = 0;
        searchFrame.Y = 0;
        searchFrame.Width = Dim.Fill();
        searchFrame.Height = 3;
        
        searchFrame.Add(ThemeManager.CreateStyledLabel("Search Users", "", 0, 0));
        
        _searchField = new TextField()
        {
            X = 1,
            Y = 1,
            Width = 30,
            Height = 1
        };
        _searchField.TextChanged += OnSearchTextChanged;
        
        var searchButton = ThemeManager.CreateStyledButton("Search", "🔍 ");
        searchButton.X = 32;
        searchButton.Y = 1;
        searchButton.MouseClick += (s, e) => SearchUsers();
        
        _refreshButton = ThemeManager.CreateStyledButton("Refresh", "🔄 ");
        _refreshButton.X = 42;
        _refreshButton.Y = 1;
        _refreshButton.MouseClick += (s, e) => LoadUsers();
        
        searchFrame.Add(_searchField, searchButton, _refreshButton);

        // Enhanced users list
        var usersFrame = ThemeManager.CreateStyledFrame("Users", ThemeManager.ComponentStyles.UserPrefix);
        usersFrame.X = 0;
        usersFrame.Y = Pos.Bottom(searchFrame);
        usersFrame.Width = Dim.Percent(70);
        usersFrame.Height = 18;
        
        usersFrame.Add(ThemeManager.CreateStyledLabel("Users", "", 0, 0));
        
        _usersList = new ListView()
        {
            X = 1,
            Y = 1,
            Width = 50,
            Height = 16
        };
        _usersList.SelectedItemChanged += OnUserSelected;
        
        usersFrame.Add(_usersList);

        // Action buttons panel
        var actionsFrame = new FrameView()
        {
            X = Pos.Right(usersFrame),
            Y = Pos.Bottom(searchFrame),
            Width = Dim.Fill(),
            Height = 18
        };
        actionsFrame.Add(new Label { X = 0, Y = 0, Text = "Actions" });
        
        _editButton = new Button()
        {
            X = 1,
            Y = 2,
            Text = "Edit User",
            Enabled = false
        };
        _editButton.MouseClick += (s, e) => EditUser();
        
        _lockButton = new Button()
        {
            X = 1,
            Y = 4,
            Text = "Lock User",
            Enabled = false
        };
        _lockButton.MouseClick += (s, e) => LockUser();
        
        _unlockButton = new Button()
        {
            X = 1,
            Y = 6,
            Text = "Unlock User",
            Enabled = false
        };
        _unlockButton.MouseClick += (s, e) => UnlockUser();
        
        _deleteButton = new Button()
        {
            X = 1,
            Y = 8,
            Text = "Delete User",
            Enabled = false
        };
        _deleteButton.MouseClick += (s, e) => DeleteUser();
        
        var auditButton = new Button()
        {
            X = 1,
            Y = 10,
            Text = "View Audit",
            Enabled = false
        };
        auditButton.MouseClick += (s, e) => ViewAuditLog();
        
        var closeButton = new Button()
        {
            X = 1,
            Y = 14,
            Text = "Close"
        };
        closeButton.MouseClick += (s, e) => Close();
        
        actionsFrame.Add(_editButton, _lockButton, _unlockButton, _deleteButton, auditButton, closeButton);

        // Status bar
        _statusLabel = new Label()
        {
            X = 0,
            Y = Pos.Bottom(usersFrame),
            Width = Dim.Fill(),
            Height = 1,
            Text = "Ready"
        };

        Add(searchFrame, usersFrame, actionsFrame, _statusLabel);
    }

    private async void LoadUsers()
    {
        try
        {
            _statusLabel!.Text = "Loading users...";
            _users = (await _userService.GetUsersAsync(0, 100)).ToList();
            UpdateUsersList();
            _statusLabel.Text = $"Loaded {_users.Count} users";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading users");
            _statusLabel!.Text = "Error loading users";
        }
    }

    private async void SearchUsers()
    {
        try
        {
            var searchTerm = _searchField?.Text?.ToString()?.Trim();
            if (string.IsNullOrEmpty(searchTerm))
            {
                LoadUsers();
                return;
            }

            _statusLabel!.Text = "Searching users...";
            _users = (await _userService.SearchUsersAsync(searchTerm, 0, 100)).ToList();
            UpdateUsersList();
            _statusLabel.Text = $"Found {_users.Count} users matching '{searchTerm}'";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error searching users");
            _statusLabel!.Text = "Error searching users";
        }
    }

    private void OnSearchTextChanged(object? sender, EventArgs e)
    {
        // Auto-search after a delay could be implemented here
    }

    private void UpdateUsersList()
    {
        if (_usersList != null)
        {
            var userItems = _users.Select(u => 
                $"{u.Handle,-20} {u.SecurityLevel,-12} {(u.IsLocked ? "[LOCKED]" : u.IsActive ? "[ACTIVE]" : "[INACTIVE]"),-10} {u.LastLoginAt?.ToString("MM/dd/yy") ?? "Never"}"
            ).ToList();
            
            if (userItems.Count == 0)
            {
                userItems.Add("No users found");
            }
            
            var observableItems = new ObservableCollection<string>(userItems);
            _usersList.SetSource<string>(observableItems);
        }
    }

    private void OnUserSelected(object? sender, ListViewItemEventArgs args)
    {
        if (args.Item >= 0 && args.Item < _users.Count)
        {
            _selectedUser = _users[args.Item];
            UpdateButtonStates();
        }
        else
        {
            _selectedUser = null;
            UpdateButtonStates();
        }
    }

    private void UpdateButtonStates()
    {
        var hasSelection = _selectedUser != null;
        
        if (_editButton != null) _editButton.Enabled = hasSelection;
        if (_lockButton != null) _lockButton.Enabled = hasSelection && !_selectedUser?.IsLocked == true;
        if (_unlockButton != null) _unlockButton.Enabled = hasSelection && _selectedUser?.IsLocked == true;
        if (_deleteButton != null) _deleteButton.Enabled = hasSelection;
    }

    private void EditUser()
    {
        if (_selectedUser == null) return;
        
        var editDialog = new UserEditDialog(_selectedUser, _userService, _logger);
        editDialog.UserUpdated += (s, e) => 
        {
            LoadUsers(); // Refresh the list
        };
        
        Application.Run(editDialog);
    }

    private async void LockUser()
    {
        if (_selectedUser == null) return;
        
        try
        {
            var success = await _userService.LockUserAsync(_selectedUser.Id, TimeSpan.FromHours(24), "Locked by admin", 1, "127.0.0.1");
            if (success)
            {
                _statusLabel!.Text = $"User {_selectedUser.Handle} has been locked";
                LoadUsers();
            }
            else
            {
                _statusLabel!.Text = $"Failed to lock user {_selectedUser.Handle}";
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error locking user {UserId}", _selectedUser.Id);
            _statusLabel!.Text = $"Error locking user {_selectedUser.Handle}";
        }
    }

    private async void UnlockUser()
    {
        if (_selectedUser == null) return;
        
        try
        {
            var success = await _userService.UnlockUserAsync(_selectedUser.Id, 1, "127.0.0.1");
            if (success)
            {
                _statusLabel!.Text = $"User {_selectedUser.Handle} has been unlocked";
                LoadUsers();
            }
            else
            {
                _statusLabel!.Text = $"Failed to unlock user {_selectedUser.Handle}";
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error unlocking user {UserId}", _selectedUser.Id);
            _statusLabel!.Text = $"Error unlocking user {_selectedUser.Handle}";
        }
    }

    private void DeleteUser()
    {
        if (_selectedUser == null) return;
        
        // Show confirmation dialog
        var result = MessageBox.Query("Confirm Delete", 
            $"Are you sure you want to delete user '{_selectedUser.Handle}'?\n\nThis action cannot be undone.", 
            "Yes", "No");
        
        if (result == 0) // Yes
        {
            // TODO: Implement user deletion when available in UserService
            _statusLabel!.Text = $"User deletion not implemented yet";
        }
    }

    private void ViewAuditLog()
    {
        if (_selectedUser == null) return;
        
        var auditDialog = new UserAuditDialog(_selectedUser, _auditService, _logger);
        Application.Run(auditDialog);
    }

    private void Close()
    {
        Application.RequestStop();
    }
}

// Simple user edit dialog
public class UserEditDialog : Dialog
{
    private readonly UserProfileDto _user;
    private readonly IUserService _userService;
    private readonly ILogger _logger;
    
    private TextField? _handleField;
    private TextField? _emailField;
    private TextField? _firstNameField;
    private TextField? _lastNameField;
    private TextField? _locationField;
    private ComboBox? _securityLevelCombo;
    
    public event EventHandler? UserUpdated;

    public UserEditDialog(UserProfileDto user, IUserService userService, ILogger logger) : base()
    {
        _user = user;
        _userService = userService;
        _logger = logger;
        
        Title = "Edit User";
        _logger = logger;
        
        Width = 60;
        Height = 20;
        
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        // Handle
        Add(new Label() { X = 1, Y = 1, Text = "Handle:" });
        _handleField = new TextField()
        {
            X = 15,
            Y = 1,
            Width = 30,
            Text = _user.Handle
        };
        Add(_handleField);

        // Email
        Add(new Label() { X = 1, Y = 3, Text = "Email:" });
        _emailField = new TextField()
        {
            X = 15,
            Y = 3,
            Width = 30,
            Text = _user.Email ?? ""
        };
        Add(_emailField);

        // First Name
        Add(new Label() { X = 1, Y = 5, Text = "First Name:" });
        _firstNameField = new TextField()
        {
            X = 15,
            Y = 5,
            Width = 30,
            Text = _user.FirstName ?? ""
        };
        Add(_firstNameField);

        // Last Name
        Add(new Label() { X = 1, Y = 7, Text = "Last Name:" });
        _lastNameField = new TextField()
        {
            X = 15,
            Y = 7,
            Width = 30,
            Text = _user.LastName ?? ""
        };
        Add(_lastNameField);

        // Location
        Add(new Label() { X = 1, Y = 9, Text = "Location:" });
        _locationField = new TextField()
        {
            X = 15,
            Y = 9,
            Width = 30,
            Text = _user.Location ?? ""
        };
        Add(_locationField);

        // Security Level
        Add(new Label() { X = 1, Y = 11, Text = "Security Level:" });
        _securityLevelCombo = new ComboBox()
        {
            X = 15,
            Y = 11,
            Width = 15,
            Height = 5
        };
        
        var securityLevels = new ObservableCollection<string>(
            Enum.GetValues<SecurityLevel>().Select(sl => sl.ToString())
        );
        _securityLevelCombo.SetSource(securityLevels);
        _securityLevelCombo.SelectedItem = (int)_user.SecurityLevel + 1; // Adjust for Banned = -1
        Add(_securityLevelCombo);

        // Buttons
        var saveButton = new Button()
        {
            X = 5,
            Y = 15,
            Text = "Save"
        };
        saveButton.MouseClick += (s, e) => SaveUser();
        
        var cancelButton = new Button()
        {
            X = 15,
            Y = 15,
            Text = "Cancel"
        };
        cancelButton.MouseClick += (s, e) => Close();
        
        Add(saveButton, cancelButton);
    }

    private async void SaveUser()
    {
        try
        {
            var updateDto = new UserUpdateDto
            {
                Email = _emailField?.Text?.ToString(),
                FirstName = _firstNameField?.Text?.ToString(),
                LastName = _lastNameField?.Text?.ToString(),
                Location = _locationField?.Text?.ToString()
            };
            
            var success = await _userService.UpdateUserProfileAsync(_user.Id, updateDto, "127.0.0.1");
            
            if (success)
            {
                // Update security level if changed
                var selectedSecurityLevel = (SecurityLevel)(_securityLevelCombo?.SelectedItem - 1 ?? 0);
                if (selectedSecurityLevel != _user.SecurityLevel)
                {
                    await _userService.SetUserSecurityLevelAsync(_user.Id, selectedSecurityLevel, 1, "127.0.0.1");
                }
                
                UserUpdated?.Invoke(this, EventArgs.Empty);
                Close();
            }
            else
            {
                MessageBox.ErrorQuery("Error", "Failed to update user", "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error updating user {UserId}", _user.Id);
            MessageBox.ErrorQuery("Error", $"Error updating user: {ex.Message}", "OK");
        }
    }

    private void Close()
    {
        Application.RequestStop();
    }
}

// Simple audit log viewer dialog
public class UserAuditDialog : Dialog
{
    private readonly UserProfileDto _user;
    private readonly IAuditService _auditService;
    private readonly ILogger _logger;
    
    private ListView? _auditList;

    public UserAuditDialog(UserProfileDto user, IAuditService auditService, ILogger logger) : base()
    {
        _user = user;
        _auditService = auditService;
        _logger = logger;
        
        Title = $"Audit Log - {user.Handle}";
        _logger = logger;
        
        Width = 70;
        Height = 20;
        
        InitializeComponent();
        LoadAuditLog();
    }

    private void InitializeComponent()
    {
        _auditList = new ListView()
        {
            X = 1,
            Y = 1,
            Width = 65,
            Height = 15
        };
        
        var closeButton = new Button()
        {
            X = 30,
            Y = 17,
            Text = "Close"
        };
        closeButton.MouseClick += (s, e) => Application.RequestStop();
        
        Add(_auditList, closeButton);
    }

    private async void LoadAuditLog()
    {
        try
        {
            var auditLogs = await _auditService.GetUserAuditLogsAsync(_user.Id, 50);
            
            var auditItems = auditLogs.Select(log => 
                $"{log.CreatedAt:MM/dd/yy HH:mm} {log.Action,-20} {log.IpAddress ?? "N/A"}"
            ).ToList();
            
            if (auditItems.Count == 0)
            {
                auditItems.Add("No audit logs found for this user");
            }
            
            var observableItems = new ObservableCollection<string>(auditItems);
            _auditList!.SetSource<string>(observableItems);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading audit log for user {UserId}", _user.Id);
        }
    }
}
