using System.ComponentModel;
using System.Windows;

namespace ClickDoc.Editor;

public partial class EditorWindow : Window
{
    readonly AppState _state;
    bool _allowClose;

    internal EditorWindow(AppState state)
    {
        _state = state;
        InitializeComponent();
        TitleBox.Text = state.Tutorial.Title;
        TitleBox.TextChanged += (_, _) =>
        {
            if (TitleBox.Text == _state.Tutorial.Title) return;
            _state.Tutorial.Title = TitleBox.Text;
            _state.Touch();
        };
        state.TutorialChanged += Refresh;
        state.PropertyChanged += OnStateChanged;
        Refresh();
        UpdateRecordButton();
    }

    /// Lets the window close for real when the app quits; otherwise closing only hides it.
    internal void AllowClose() => _allowClose = true;

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }

    void Refresh()
    {
        if (!TitleBox.IsKeyboardFocused && TitleBox.Text != _state.Tutorial.Title) TitleBox.Text = _state.Tutorial.Title;
        EmptyHint.Visibility = _state.Tutorial.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppState.IsRecording)) UpdateRecordButton();
    }

    void UpdateRecordButton() => RecordButton.Content = _state.IsRecording ? L("停止录制") : L("开始录制");

    void OnRecordClick(object sender, RoutedEventArgs e)
    {
        if (_state.IsRecording) _state.StopRecording();
        else _state.StartRecording();
    }
}
