using System.Collections.ObjectModel;
using Blackboard.Core.DTOs;
using Blackboard.Core.Services;
using Serilog;
using Terminal.Gui.Views;

namespace Blackboard.UI.Admin;

public class FileAreaManagementWindow : Window
{
    private readonly IFileAreaService _fileAreaService;
    private readonly ILogger _logger;
    private ListView _areaListView = null!;
    private Button _createAreaButton = null!;
    private Button _deleteAreaButton = null!;
    private Button _editAreaButton = null!;

    private List<FileAreaDto> _fileAreas = new();
    private ListView _fileListView = null!;
    private List<BbsFileDto> _pendingFiles = new();
    private Button _refreshButton = null!;
    private FileAreaDto? _selectedArea;
    private Label _statisticsLabel = null!;
    private Label _statusLabel = null!;

    public FileAreaManagementWindow(IFileAreaService fileAreaService, ILogger logger)
    {
        _fileAreaService = fileAreaService;
        _logger = logger;

        Title = "File Area Management";
        X = 0;
        Y = 0;
        Width = 80;
        Height = 25;

        InitializeComponents();
        LoadData();
    }

    private void InitializeComponents()
    {
        _statisticsLabel = new Label
        {
            X = 1,
            Y = 1,
            Text = "Loading statistics..."
        };
        Add(_statisticsLabel);

        // File Areas section
        var areaLabel = new Label { X = 1, Y = 3, Text = "File Areas:" };
        Add(areaLabel);

        _areaListView = new ListView
        {
            X = 1,
            Y = 4,
            Width = 38,
            Height = 10
        };
        Add(_areaListView);

        // Files section  
        var fileLabel = new Label { X = 41, Y = 3, Text = "Pending Files:" };
        Add(fileLabel);

        _fileListView = new ListView
        {
            X = 41,
            Y = 4,
            Width = 38,
            Height = 10
        };
        Add(_fileListView);

        // Buttons
        _refreshButton = new Button { X = 1, Y = 15, Text = "Refresh" };
        _refreshButton.MouseClick += (s, e) => LoadData();
        Add(_refreshButton);

        _createAreaButton = new Button { X = 12, Y = 15, Text = "Create" };
        _createAreaButton.MouseClick += (s, e) => CreateArea();
        Add(_createAreaButton);

        _editAreaButton = new Button { X = 22, Y = 15, Text = "Edit" };
        _editAreaButton.MouseClick += (s, e) => EditArea();
        Add(_editAreaButton);

        _deleteAreaButton = new Button { X = 30, Y = 15, Text = "Delete" };
        _deleteAreaButton.MouseClick += (s, e) => DeleteArea();
        Add(_deleteAreaButton);

        _statusLabel = new Label
        {
            X = 1,
            Y = 23,
            Text = "Ready"
        };
        Add(_statusLabel);
    }

    private async void LoadData()
    {
        try
        {
            _statusLabel.Text = "Loading data...";

            // Load file areas
            _fileAreas = (await _fileAreaService.GetAllFileAreasAsync()).ToList();
            var areaItems = _fileAreas.Select(area =>
                $"{area.Name} ({area.FileCount} files)").ToList();
            _areaListView.SetSource<string>(new ObservableCollection<string>(areaItems));

            // Load pending files
            _pendingFiles = (await _fileAreaService.GetPendingApprovalFilesAsync()).ToList();
            var fileItems = _pendingFiles.Select(file =>
                $"{file.OriginalFileName} - {file.AreaName}").ToList();
            _fileListView.SetSource<string>(new ObservableCollection<string>(fileItems));

            // Load statistics
            var stats = await _fileAreaService.GetFileAreaStatisticsAsync();
            _statisticsLabel.Text = $"Areas: {stats.TotalAreas} | Files: {stats.TotalFiles} | Pending: {stats.PendingApproval}";

            _statusLabel.Text = $"Loaded {_fileAreas.Count} areas, {_pendingFiles.Count} pending files";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error loading file area data");
            _statusLabel.Text = "Error loading data";
        }
    }

    private void CreateArea()
    {
        // For now, just show a placeholder message
        _statusLabel.Text = "Create area feature - coming soon";
    }

    private void EditArea()
    {
        if (_areaListView.SelectedItem < 0 || _areaListView.SelectedItem >= _fileAreas.Count)
        {
            _statusLabel.Text = "Please select an area to edit";
            return;
        }

        _selectedArea = _fileAreas[_areaListView.SelectedItem];
        _statusLabel.Text = $"Edit area feature for '{_selectedArea.Name}' - coming soon";
    }

    private async void DeleteArea()
    {
        if (_areaListView.SelectedItem < 0 || _areaListView.SelectedItem >= _fileAreas.Count)
        {
            _statusLabel.Text = "Please select an area to delete";
            return;
        }

        var area = _fileAreas[_areaListView.SelectedItem];

        try
        {
            await _fileAreaService.DeleteFileAreaAsync(area.Id);
            _statusLabel.Text = $"Deleted area '{area.Name}'";
            LoadData();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error deleting file area {AreaId}", area.Id);
            _statusLabel.Text = "Error deleting area";
        }
    }
}