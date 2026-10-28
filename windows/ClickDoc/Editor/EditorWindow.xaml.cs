using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace ClickDoc.Editor;

public partial class EditorWindow : Window
{
    readonly AppState _state;
    readonly StepListView _list;
    bool _allowClose;
    /// The step whose detail is shown, and its description box.
    Guid? _shownStep;
    TextBox? _description;
    IReadOnlySet<Guid> _shownSelection = new HashSet<Guid>();

    internal EditorWindow(AppState state)
    {
        _state = state;
        InitializeComponent();
        _list = new StepListView(state);
        ListHost.Content = _list;
        _list.SelectionChanged += (_, _) => ShowDetail();
        _list.DeleteRequested += RequestDelete;
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
        _list.Sync();
        if (_shownStep is { } id && _state.Tutorial.Steps.FirstOrDefault(s => s.Id == id) is { } step)
        {
            if (_description is { IsKeyboardFocused: false } box && box.Text != step.Description) box.Text = step.Description;
        }
        ShowDetail();
    }

    void ShowDetail()
    {
        var selected = _list.SelectedIds;
        var steps = _state.Tutorial.Steps;
        selected = selected.Where(id => steps.Any(s => s.Id == id)).ToHashSet();
        if (selected.SetEquals(_shownSelection) && DetailHost.Content is not null) return;
        _shownSelection = selected;
        _shownStep = null;
        _description = null;
        if (selected.Count > 1) DetailHost.Content = MultiSelectionPanel(selected);
        else if (selected.Count == 1) DetailHost.Content = StepPanel(steps.First(s => s.Id == selected.First()));
        else DetailHost.Content = new TextBlock
        {
            Text = L("选择一个步骤"), Opacity = 0.6, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
    }

    UIElement MultiSelectionPanel(IReadOnlySet<Guid> selected)
    {
        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(new TextBlock { Text = L("已选择 %lld 个步骤", selected.Count), FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center });
        var delete = new Button { Content = L("删除这 %lld 个步骤", selected.Count), Margin = new Thickness(0, 12, 0, 12), HorizontalAlignment = HorizontalAlignment.Center };
        delete.Click += (_, _) => RequestDelete(selected);
        panel.Children.Add(delete);
        panel.Children.Add(new TextBlock { Text = L("按住 Ctrl 点选多个，按住 Shift 连续选择，Ctrl+A 全选"), FontSize = 11, Opacity = 0.6, HorizontalAlignment = HorizontalAlignment.Center });
        return panel;
    }

    UIElement StepPanel(Step step)
    {
        _shownStep = step.Id;
        var id = step.Id;
        var panel = new DockPanel();
        var bottom = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        DockPanel.SetDock(bottom, Dock.Bottom);
        bottom.Children.Add(new TextBlock { Text = L("描述"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) });
        var description = new TextBox
        {
            Text = step.Description, Height = 80, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        description.TextChanged += (_, _) =>
        {
            if (_state.Tutorial.Steps.FirstOrDefault(s => s.Id == id) is { } current && current.Description != description.Text)
                _state.SetDescription(id, description.Text);
        };
        _description = description;
        bottom.Children.Add(description);
        panel.Children.Add(bottom);
        panel.Children.Add(StepImage(step));
        return panel;
    }

    UIElement StepImage(Step step)
    {
        try { return new Image { Source = Bitmaps.FromPng(step.Png), Stretch = System.Windows.Media.Stretch.Uniform }; }
        catch (Exception) { return new TextBlock { Text = ContentLanguage.Current.ImageProcessingFailed }; }
    }

    /// Deleting several steps asks first; there is no undo.
    void RequestDelete(IReadOnlySet<Guid> ids)
    {
        if (ids.Count == 0) return;
        if (ids.Count > 1)
        {
            var answer = MessageBox.Show(this, L("删除后无法恢复。"), L("删除 %lld 个步骤？", ids.Count),
                                         MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel);
            if (answer != MessageBoxResult.OK) return;
        }
        var next = Selection.AfterDeleting(ids, _state.Tutorial.Steps);
        _state.DeleteSteps(ids);
        _list.Select(next is { } n ? [n] : []);
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
