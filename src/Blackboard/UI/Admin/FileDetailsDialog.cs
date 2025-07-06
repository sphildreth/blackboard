using Blackboard.Core.DTOs;
using Terminal.Gui.Views;

namespace Blackboard.UI.Admin;

public class FileDetailsDialog : Window
{
    private readonly BbsFileDto _file;
    private Button _closeButton = null!;
    private TextView _detailsView = null!;

    public FileDetailsDialog(BbsFileDto file)
    {
        _file = file;

        Title = $"File Details - {file.OriginalFileName}";
        X = 5;
        Y = 3;
        Width = 70;
        Height = 20;

        InitializeComponents();
        LoadFileDetails();
    }

    private void InitializeComponents()
    {
        _detailsView = new TextView
        {
            X = 1,
            Y = 1,
            Width = 65,
            Height = 15,
            ReadOnly = true
        };
        Add(_detailsView);

        _closeButton = new Button { X = 30, Y = 17, Text = "Close" };
        _closeButton.MouseClick += (s, e) => RequestStop();
        Add(_closeButton);
    }

    private void LoadFileDetails()
    {
        var details = $@"File Name: {_file.OriginalFileName}
System Name: {_file.FileName}
Area: {_file.AreaName}
Size: {_file.SizeFormatted}
Upload Date: {_file.UploadDate:yyyy-MM-dd HH:mm:ss}
Uploader: {_file.UploaderHandle ?? "Unknown"}
Approved: {(_file.IsApproved ? "Yes" : "No")}
Active: {(_file.IsActive ? "Yes" : "No")}
Download Count: {_file.DownloadCount}
Rating: {_file.AverageRating:F1}/5.0 ({_file.RatingCount} ratings)

Description:
{_file.Description ?? "No description provided."}

Tags: {string.Join(", ", _file.Tags ?? Array.Empty<string>())}

File Path: {_file.FilePath}
";

        _detailsView.Text = details;
    }
}