using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ClickDoc.Editor;

public partial class EditorWindow : Window
{
    readonly AppState _state;
    readonly StepListView _list;
    bool _allowClose;
    /// The step whose detail is shown, and its description box.
    Guid? _shownStep;
    TextBox? _description;
    AnnotationCanvas? _canvas;
    /// Refreshes the shown step's toolbar buttons after tutorial changes.
    Action? _refreshToolbar;
    EditorTool _tool = EditorTool.Select;
    IReadOnlySet<Guid> _shownSelection = new HashSet<Guid>();

    static readonly (EditorTool Tool, string Title, string Hint)[] Tools =
    [
        (EditorTool.Select, L("选择"), L("点选标注可移动，拖动手柄可调整，按 Delete 删除；双击文字可修改")),
        (EditorTool.Blur, L("模糊"), L("拖动框选要模糊的区域")),
        (EditorTool.Rectangle, L("方框"), L("拖动画出方框")),
        (EditorTool.Arrow, L("箭头"), L("从起点拖到终点画出箭头")),
        (EditorTool.Text, L("文字"), L("在图上点一下输入文字，回车确认，Esc 取消")),
    ];

    internal EditorWindow(AppState state)
    {
        _state = state;
        InitializeComponent();
        _list = new StepListView(state);
        ListHost.Content = _list;
        ExportSlot.Content = new ExportMenu(state, this);
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
        state.CommitPendingEdits += () => _canvas?.CommitText();
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
        ExportSlot.IsEnabled = !_state.Tutorial.IsEmpty;
        _list.Sync();
        if (_shownStep is { } id && _state.Tutorial.Steps.FirstOrDefault(s => s.Id == id) is { } step)
        {
            if (_description is { IsKeyboardFocused: false } box && box.Text != step.Description) box.Text = step.Description;
            _canvas?.Update(step.BlurRegions, step.Annotations);
            _refreshToolbar?.Invoke();
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
        _canvas?.CommitText();
        _shownStep = null;
        _description = null;
        _canvas = null;
        _refreshToolbar = null;
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
        try
        {
            var canvas = new AnnotationCanvas(step.Png, step.BlurRegions, step.Annotations) { Tool = _tool };
            canvas.BlurCommitted += regions => _state.SetBlurRegions(id, regions);
            canvas.AnnotationsCommitted += marks => _state.SetAnnotations(id, marks);
            _canvas = canvas;
            var toolbar = Toolbar(canvas, id);
            DockPanel.SetDock(toolbar, Dock.Top);
            panel.Children.Add(toolbar);
            panel.Children.Add(canvas);
        }
        catch (Exception)
        {
            panel.Children.Add(new TextBlock { Text = ContentLanguage.Current.ImageProcessingFailed });
        }
        return panel;
    }

    /// Tool buttons, delete-selected and clear-all, with the current tool's hint.
    FrameworkElement Toolbar(AnnotationCanvas canvas, Guid id)
    {
        var bar = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        var hint = new TextBlock { FontSize = 11, Opacity = 0.6, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        var buttons = new List<(ToggleButton Button, EditorTool Tool, string Hint)>();
        void SelectTool(EditorTool tool)
        {
            _tool = tool;
            canvas.Tool = tool;
            foreach (var (button, t, h) in buttons)
            {
                button.IsChecked = t == tool;
                if (t == tool) hint.Text = h;
            }
        }
        foreach (var (tool, title, toolHint) in Tools)
        {
            var button = new ToggleButton { Content = title, MinWidth = 56, Margin = new Thickness(0, 0, 2, 0) };
            button.Click += (_, _) => SelectTool(tool);
            buttons.Add((button, tool, toolHint));
            bar.Children.Add(button);
        }
        var deleteSelected = new Button { Content = L("删除选中"), Margin = new Thickness(10, 0, 0, 0), IsEnabled = false };
        deleteSelected.Click += (_, _) => canvas.DeleteSelected();
        var clearAll = new Button { Content = L("清除全部"), Margin = new Thickness(6, 0, 0, 0) };
        clearAll.Click += (_, _) =>
        {
            _state.SetBlurRegions(id, []);
            _state.SetAnnotations(id, []);
        };
        void UpdateButtons()
        {
            deleteSelected.IsEnabled = canvas.Selected is not null;
            clearAll.IsEnabled = _state.Tutorial.Steps.FirstOrDefault(s => s.Id == id) is { } s && (s.BlurRegions.Count > 0 || s.Annotations.Count > 0);
        }
        canvas.SelectionChanged += UpdateButtons;
        _refreshToolbar = UpdateButtons;
        bar.Children.Add(deleteSelected);
        bar.Children.Add(clearAll);
        bar.Children.Add(hint);
        SelectTool(_tool);
        UpdateButtons();
        return bar;
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
