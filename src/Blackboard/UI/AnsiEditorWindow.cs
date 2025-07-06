using Terminal.Gui.Views;

namespace Blackboard.UI;

public class AnsiEditorWindow : Window
{
    private readonly TextView _editor;
    private readonly Action<string>? _onSave;

    public AnsiEditorWindow(string title = "ANSI Message Editor", Action<string>? onSave = null)
    {
        _onSave = onSave;
        Title = title;
        X = 0;
        Y = 0;
        Width = 80;
        Height = 25;

        _editor = new TextView
        {
            X = 1,
            Y = 1,
            Width = 76,
            Height = 20,
            WordWrap = false,
            AllowsTab = true
        };
        Add(_editor);

        var saveBtn = new Button
        {
            Text = "Save",
            X = 10,
            Y = 22
        };
        saveBtn.MouseClick += (s, e) => OnSaveClicked();
        Add(saveBtn);
    }

    public string ComposedText { get; private set; } = string.Empty;

    private void OnSaveClicked()
    {
        ComposedText = _editor.Text;
        _onSave?.Invoke(ComposedText);
        RequestStop();
    }

    public void SetText(string text)
    {
        _editor.Text = text;
    }
}