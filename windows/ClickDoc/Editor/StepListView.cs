using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ClickDoc.Editor;

/// One row of the step list.
sealed class StepRow(Guid id) : INotifyPropertyChanged
{
    string _heading = "", _caption = "";
    ImageSource? _thumbnail;

    public Guid Id { get; } = id;
    public string Heading { get => _heading; set => Set(ref _heading, value); }
    public string Caption { get => _caption; set => Set(ref _caption, value); }
    public ImageSource? Thumbnail { get => _thumbnail; set => Set(ref _thumbnail, value); }

    public event PropertyChangedEventHandler? PropertyChanged;

    void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// The step list: thumbnails, multi-select, delete, and drag to reorder.
sealed class StepListView : ListBox
{
    const string DragFormat = "ClickDoc.StepIndices";

    readonly AppState _state;
    readonly ObservableCollection<StepRow> _rows = [];
    /// Thumbnail per step, valid while the step's blur regions and annotations are the same lists.
    readonly Dictionary<Guid, (object Blur, object Marks, ImageSource Image)> _thumbnails = [];
    Point? _dragStart;
    InsertionAdorner? _insertion;

    public StepListView(AppState state)
    {
        _state = state;
        SelectionMode = SelectionMode.Extended;
        AllowDrop = true;
        ItemsSource = _rows;
        ItemTemplate = BuildTemplate();
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        ScrollViewer.SetHorizontalScrollBarVisibility(this, ScrollBarVisibility.Disabled);
        ContextMenu = new ContextMenu();
        ContextMenuOpening += OnContextMenuOpening;
        Sync();
    }

    /// Asked to delete steps; the window confirms multi-step deletes.
    public event Action<IReadOnlySet<Guid>>? DeleteRequested;

    public IReadOnlySet<Guid> SelectedIds => SelectedItems.Cast<StepRow>().Select(r => r.Id).ToHashSet();

    public void Select(IEnumerable<Guid> ids)
    {
        var wanted = ids.ToHashSet();
        SelectedItems.Clear();
        foreach (var row in _rows.Where(r => wanted.Contains(r.Id))) SelectedItems.Add(row);
        if (SelectedItems.Count > 0) ScrollIntoView(SelectedItems[0]);
    }

    /// Brings rows in line with the tutorial, keeping the selection when the same steps remain.
    public void Sync()
    {
        var steps = _state.Tutorial.Steps;
        if (!steps.Select(s => s.Id).SequenceEqual(_rows.Select(r => r.Id)))
        {
            var selected = SelectedIds;
            _rows.Clear();
            foreach (var step in steps) _rows.Add(new StepRow(step.Id));
            foreach (var row in _rows.Where(r => selected.Contains(r.Id))) SelectedItems.Add(row);
            foreach (var id in _thumbnails.Keys.Except(steps.Select(s => s.Id)).ToList()) _thumbnails.Remove(id);
        }
        for (int i = 0; i < steps.Count; i++)
        {
            var row = _rows[i];
            row.Heading = L("步骤 %lld", i + 1);
            row.Caption = steps[i].Description.Split('\n')[0];
            row.Thumbnail = Thumbnail(steps[i]);
        }
    }

    ImageSource? Thumbnail(Step step)
    {
        if (_thumbnails.TryGetValue(step.Id, out var cached) && cached.Blur == step.BlurRegions && cached.Marks == step.Annotations)
            return cached.Image;
        try
        {
            using var small = Imaging.Thumbnail(step.Png, 240);
            int fullWidth = Imaging.DecodeWidth(step.Png);
            using var composed = Annotations.Compose(small, step, fullWidth);
            var image = Bitmaps.ToImageSource(composed);
            _thumbnails[step.Id] = (step.BlurRegions, step.Annotations, image);
            return image;
        }
        catch (ExportException)
        {
            return null;
        }
    }

    static DataTemplate BuildTemplate()
    {
        var grid = new FrameworkElementFactory(typeof(DockPanel));
        grid.SetValue(MarginProperty, new Thickness(0, 3, 0, 3));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        border.SetValue(WidthProperty, 80.0);
        border.SetValue(HeightProperty, 48.0);
        border.SetValue(DockPanel.DockProperty, Dock.Left);
        border.SetValue(Border.ClipToBoundsProperty, true);
        var image = new FrameworkElementFactory(typeof(Image));
        image.SetBinding(Image.SourceProperty, new System.Windows.Data.Binding(nameof(StepRow.Thumbnail)));
        image.SetValue(Image.StretchProperty, Stretch.Uniform);
        border.AppendChild(image);
        grid.AppendChild(border);
        var text = new FrameworkElementFactory(typeof(StackPanel));
        text.SetValue(MarginProperty, new Thickness(10, 0, 0, 0));
        text.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        var heading = new FrameworkElementFactory(typeof(TextBlock));
        heading.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(StepRow.Heading)));
        heading.SetValue(TextBlock.FontSizeProperty, 11.0);
        heading.SetValue(OpacityProperty, 0.65);
        var caption = new FrameworkElementFactory(typeof(TextBlock));
        caption.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(StepRow.Caption)));
        caption.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        text.AppendChild(heading);
        text.AppendChild(caption);
        grid.AppendChild(text);
        return new DataTemplate { VisualTree = grid };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Delete && SelectedItems.Count > 0)
        {
            DeleteRequested?.Invoke(SelectedIds);
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        var menu = ContextMenu!;
        menu.Items.Clear();
        if (RowAt(e.OriginalSource as DependencyObject) is not { } clicked)
        {
            e.Handled = true;
            return;
        }
        var targets = Selection.DeletionTargets(clicked.Id, SelectedIds);
        var delete = new MenuItem { Header = new TextBlock { Text = targets.Count > 1 ? L("删除所选的 %lld 个步骤", targets.Count) : L("删除") } };
        delete.Click += (_, _) => DeleteRequested?.Invoke(targets);
        var selectAll = new MenuItem { Header = new TextBlock { Text = L("全选") } };
        selectAll.Click += (_, _) => SelectAll();
        menu.Items.Add(delete);
        menu.Items.Add(new Separator());
        menu.Items.Add(selectAll);
    }

    static StepRow? RowAt(DependencyObject? source)
    {
        while (source is not null and not ListBoxItem) source = VisualTreeHelper.GetParent(source);
        return (source as ListBoxItem)?.DataContext as StepRow;
    }

    // Drag to reorder. Dragging moves the selected rows; WPF has already selected the pressed row.

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        _dragStart = RowAt(e.OriginalSource as DependencyObject) is null ? null : e.GetPosition(this);
        base.OnPreviewMouseLeftButtonDown(e);
    }

    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        if (_dragStart is not { } start || e.LeftButton != MouseButtonState.Pressed || SelectedItems.Count == 0) return;
        var delta = e.GetPosition(this) - start;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        _dragStart = null;
        var indices = SelectedItems.Cast<StepRow>().Select(r => _rows.IndexOf(r)).Order().ToArray();
        DragDrop.DoDragDrop(this, new DataObject(DragFormat, indices), DragDropEffects.Move);
        RemoveInsertion();
    }

    protected override void OnDragOver(DragEventArgs e)
    {
        base.OnDragOver(e);
        if (!e.Data.GetDataPresent(DragFormat))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }
        e.Effects = DragDropEffects.Move;
        ShowInsertion(InsertionIndex(e));
        e.Handled = true;
    }

    protected override void OnDragLeave(DragEventArgs e)
    {
        base.OnDragLeave(e);
        RemoveInsertion();
    }

    protected override void OnDrop(DragEventArgs e)
    {
        base.OnDrop(e);
        RemoveInsertion();
        if (e.Data.GetData(DragFormat) is not int[] indices) return;
        var moving = indices.Select(i => _rows[i].Id).ToList();
        _state.MoveSteps(indices, InsertionIndex(e));
        Select(moving);
        e.Handled = true;
    }

    /// The gap the pointer is over: before the row whose upper half it is in.
    int InsertionIndex(DragEventArgs e)
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            if (ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem item) continue;
            var p = e.GetPosition(item);
            if (p.Y < item.ActualHeight / 2) return i;
        }
        return _rows.Count;
    }

    void ShowInsertion(int index)
    {
        double y;
        if (_rows.Count == 0) y = 0;
        else if (index < _rows.Count && ItemContainerGenerator.ContainerFromIndex(index) is ListBoxItem before)
            y = before.TranslatePoint(new Point(0, 0), this).Y;
        else if (ItemContainerGenerator.ContainerFromIndex(_rows.Count - 1) is ListBoxItem last)
            y = last.TranslatePoint(new Point(0, last.ActualHeight), this).Y;
        else return;
        if (_insertion is null)
        {
            var layer = AdornerLayer.GetAdornerLayer(this);
            if (layer is null) return;
            _insertion = new InsertionAdorner(this);
            layer.Add(_insertion);
        }
        _insertion.Y = y;
    }

    void RemoveInsertion()
    {
        if (_insertion is null) return;
        AdornerLayer.GetAdornerLayer(this)?.Remove(_insertion);
        _insertion = null;
    }

    /// A horizontal line marking where dropped rows will land.
    sealed class InsertionAdorner(UIElement adorned) : Adorner(adorned)
    {
        double _y;

        public double Y
        {
            get => _y;
            set
            {
                _y = value;
                InvalidateVisual();
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            var pen = new Pen(SystemColors.HighlightBrush, 2);
            dc.DrawLine(pen, new Point(4, _y), new Point(AdornedElement.RenderSize.Width - 4, _y));
        }
    }
}
