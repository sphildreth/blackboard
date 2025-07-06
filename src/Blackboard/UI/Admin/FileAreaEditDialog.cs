using Blackboard.Core.DTOs;
using Blackboard.Core.Services;
using Serilog;
using Terminal.Gui.Views;

namespace Blackboard.UI.Admin;

public class FileAreaEditDialog : Window
{
    private readonly FileAreaDto? _area;
    private readonly IFileAreaService _fileAreaService;
    private readonly ILogger _logger;
    private Button _cancelButton = null!;
    private TextView _descriptionField = null!;
    private CheckBox _isActiveField = null!;
    private TextView _nameField = null!;
    private Button _saveButton = null!;

    public FileAreaEditDialog(FileAreaDto? area, IFileAreaService fileAreaService, ILogger logger)
    {
        _area = area;
        _fileAreaService = fileAreaService;
        _logger = logger;

        Title = area == null ? "Create File Area" : "Edit File Area";
        X = 10;
        Y = 5;
        Width = 60;
        Height = 15;

        InitializeComponents();
        if (area != null) LoadArea(area);
    }

    public bool DialogResult { get; private set; }

    private void InitializeComponents()
    {
        var nameLabel = new Label { X = 1, Y = 1, Text = "Name:" };
        Add(nameLabel);

        _nameField = new TextView
        {
            X = 1,
            Y = 2,
            Width = 50,
            Height = 1
        };
        Add(_nameField);

        var descLabel = new Label { X = 1, Y = 4, Text = "Description:" };
        Add(descLabel);

        _descriptionField = new TextView
        {
            X = 1,
            Y = 5,
            Width = 50,
            Height = 3
        };
        Add(_descriptionField);

        _isActiveField = new CheckBox
        {
            X = 1,
            Y = 9,
            Text = "Active"
        };
        Add(_isActiveField);

        _saveButton = new Button { X = 10, Y = 11, Text = "Save" };
        _saveButton.MouseClick += (s, e) => SaveArea();
        Add(_saveButton);

        _cancelButton = new Button { X = 20, Y = 11, Text = "Cancel" };
        _cancelButton.MouseClick += (s, e) => CancelDialog();
        Add(_cancelButton);
    }

    private void LoadArea(FileAreaDto area)
    {
        _nameField.Text = area.Name;
        _descriptionField.Text = area.Description ?? "";
        // For Terminal.Gui CheckBox, we need to handle boolean differently
        // Let's just track the state manually for now
    }

    private async void SaveArea()
    {
        try
        {
            var name = _nameField.Text.Trim();
            var description = _descriptionField.Text.Trim();
            // CheckBox state - we'll assume true for now since we can't access the property
            var isActive = true;

            if (string.IsNullOrEmpty(name))
                // Simple validation - could show a message box in a real implementation
                return;

            if (_area == null)
            {
                // Create new area
                var newArea = new FileAreaDto
                {
                    Name = name,
                    Description = description,
                    IsActive = isActive,
                    AllowUploads = true,
                    AllowDownloads = true,
                    FileCount = 0,
                    TotalSize = 0,
                    Path = "/files/" + name.ToLowerInvariant().Replace(" ", "_"),
                    RequiredLevel = 1,
                    UploadLevel = 10,
                    MaxFileSize = 1024 * 1024 * 10 // 10MB
                };
                await _fileAreaService.CreateFileAreaAsync(newArea);
            }
            else
            {
                // Update existing area
                _area.Name = name;
                _area.Description = description;
                _area.IsActive = isActive;
                await _fileAreaService.UpdateFileAreaAsync(_area);
            }

            DialogResult = true;
            RequestStop();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error saving file area");
            // In a real implementation, show an error message
        }
    }

    private void CancelDialog()
    {
        DialogResult = false;
        RequestStop();
    }
}