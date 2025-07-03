using System;
using Terminal.Gui.Views;

namespace Blackboard.UI
{
    public class AnsiEditorWindow : Window
    {
        public string ComposedText { get; private set; } = string.Empty;
        private TextView _editor;
        private Action<string>? _onSave;

        public AnsiEditorWindow(string title = "ANSI Message Editor", Action<string>? onSave = null) : base(title)
        {
            _onSave = onSave;
            Width = Dim.Fill();
            Height = Dim.Fill();

            _editor = new TextView
            {
                X = 1,
                Y = 1,
                Width = Dim.Fill() - 2,
                Height = Dim.Fill() - 3,
                WordWrap = false,
                AllowsTab = true
            };
            Add(_editor);

            var saveBtn = new Button()
            {
                Text = "Save",
                X = 10,
                Y = 20
            };
            saveBtn.MouseClick += (s, e) => OnSaveClicked();
            Add(saveBtn);
        }

        private void OnSaveClicked()
        {
            ComposedText = _editor.Text.ToString();
            _onSave?.Invoke(ComposedText);
            this.RequestStop();
        }

        public void SetText(string text)
        {
            _editor.Text = text;
        }
    }
}
