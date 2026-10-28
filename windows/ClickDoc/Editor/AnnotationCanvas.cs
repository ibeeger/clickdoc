using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using SkiaSharp;
using Geometry = ClickDoc.Core.Geometry;

namespace ClickDoc.Editor;

enum EditorTool { Select, Blur, Rectangle, Arrow, Text }

/// Shows a step's screenshot with its blur regions and annotations, and edits them with the
/// mouse. Changes are reported through the Committed events; the owner writes them back and
/// calls Update.
sealed class AnnotationCanvas : Grid
{
    const double MinSide = 4;
    static readonly Guid DraftId = Guid.NewGuid();
    static readonly SolidColorBrush Red = Frozen(Color.FromRgb(237, 28, 36));

    abstract record DragMode;
    sealed record Create(SKPoint Start) : DragMode;
    sealed record MoveBlur(int Index, SKRect Original) : DragMode;
    sealed record ResizeBlur(int Index, SKRect Original) : DragMode;
    sealed record MoveAnnotation(Guid Id, Mark Original) : DragMode;
    sealed record ResizeBox(Guid Id, SKRect Original) : DragMode;
    sealed record MoveArrowEnd(Guid Id, bool IsStart, ArrowMark Original) : DragMode;
    sealed record Idle : DragMode;

    sealed record TextEdit(Guid? Id, SKPoint Origin);

    readonly BitmapSource _image;
    readonly Canvas _surface = new() { Background = Brushes.Transparent, ClipToBounds = true, Focusable = true, FocusVisualStyle = null };
    readonly Image _base = new() { Stretch = Stretch.Fill };
    readonly Image _blurred = new() { Stretch = Stretch.Fill };
    readonly Image _marks = new() { Stretch = Stretch.Fill, IsHitTestVisible = false };
    readonly SelectionLayer _selection;
    readonly TextBox _textBox = new() { Visibility = Visibility.Collapsed, FontWeight = FontWeights.Bold, Foreground = Red, MinWidth = 140 };

    IReadOnlyList<SKRect> _blur;
    IReadOnlyList<Annotation> _annotations;
    IReadOnlyList<SKRect>? _workingBlur;
    IReadOnlyList<Annotation>? _workingAnnotations;
    DragMode? _mode;
    Point _dragStart;
    bool _doubleClick;
    TextEdit? _textEdit;
    CanvasItem? _selected;
    Size _fitted;

    public AnnotationCanvas(byte[] png, IReadOnlyList<SKRect> blur, IReadOnlyList<Annotation> annotations)
    {
        _image = Bitmaps.FromPng(png);
        _blur = blur;
        _annotations = annotations;
        _base.Source = _image;
        _blurred.Source = _image;
        _blurred.Effect = new BlurEffect { KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Performance };
        _selection = new SelectionLayer(this) { IsHitTestVisible = false };
        _surface.Children.Add(_base);
        _surface.Children.Add(_blurred);
        _surface.Children.Add(_marks);
        _surface.Children.Add(_selection);
        _surface.Children.Add(_textBox);
        Children.Add(_surface);
        _surface.HorizontalAlignment = HorizontalAlignment.Center;
        _surface.VerticalAlignment = VerticalAlignment.Center;
        SizeChanged += (_, _) => Layout();
        _surface.MouseLeftButtonDown += OnMouseDown;
        _surface.MouseMove += OnMouseMove;
        _surface.MouseLeftButtonUp += OnMouseUp;
        _surface.KeyDown += OnKeyDown;
        _textBox.KeyDown += OnTextKeyDown;
        _textBox.LostKeyboardFocus += (_, _) => { if (_textEdit is not null) CommitText(); };
        _textBox.TextChanged += (_, _) => PositionTextBox();
    }

    public EditorTool Tool { get; set; }

    public CanvasItem? Selected
    {
        get => _selected;
        private set
        {
            _selected = value;
            _selection.InvalidateVisual();
            SelectionChanged?.Invoke();
        }
    }

    public event Action? SelectionChanged;
    public event Action<IReadOnlyList<SKRect>>? BlurCommitted;
    public event Action<IReadOnlyList<Annotation>>? AnnotationsCommitted;

    float PixelWidth => Math.Max(1, _image.PixelWidth);
    SKSize PixelSize => new(PixelWidth, Math.Max(1, _image.PixelHeight));
    SKSize Fitted => new((float)_fitted.Width, (float)_fitted.Height);
    IReadOnlyList<SKRect> ShownBlur => _workingBlur ?? _blur;
    IReadOnlyList<Annotation> ShownAnnotations => _workingAnnotations ?? _annotations;

    /// New committed state from the tutorial.
    public void Update(IReadOnlyList<SKRect> blur, IReadOnlyList<Annotation> annotations)
    {
        if (ReferenceEquals(blur, _blur) && ReferenceEquals(annotations, _annotations)) return;
        _blur = blur;
        _annotations = annotations;
        // The annotation being edited was deleted from the toolbar: drop the edit.
        if (_textEdit?.Id is { } id && annotations.All(a => a.Id != id)) EndTextEdit();
        if (_selected is BlurItem b && b.Index >= blur.Count || _selected is AnnotationItem a && annotations.All(x => x.Id != a.Id)) Selected = null;
        Redraw();
    }

    public void DeleteSelected()
    {
        switch (_selected)
        {
            case BlurItem b when b.Index < _blur.Count:
                BlurCommitted?.Invoke(_blur.Where((_, i) => i != b.Index).ToList());
                break;
            case AnnotationItem a:
                AnnotationsCommitted?.Invoke(_annotations.Where(x => x.Id != a.Id).ToList());
                break;
        }
        Selected = null;
    }

    // Layout and drawing

    void Layout()
    {
        double scale = Math.Min(ActualWidth / _image.PixelWidth, ActualHeight / _image.PixelHeight);
        if (!(scale > 0)) return;
        _fitted = new Size(_image.PixelWidth * scale, _image.PixelHeight * scale);
        _surface.Width = _fitted.Width;
        _surface.Height = _fitted.Height;
        foreach (var image in new[] { _base, _blurred, _marks })
        {
            image.Width = _fitted.Width;
            image.Height = _fitted.Height;
        }
        _selection.Width = _fitted.Width;
        _selection.Height = _fitted.Height;
        ((BlurEffect)_blurred.Effect).Radius = 3 * Imaging.BlurSigma(PixelWidth) * _fitted.Width / PixelWidth;
        Redraw();
    }

    void Redraw()
    {
        if (_fitted.Width <= 0) return;
        var clip = new GeometryGroup();
        foreach (var r in ShownBlur)
        {
            var v = Geometry.Denormalize(r, Fitted);
            clip.Children.Add(new RectangleGeometry(new Rect(v.Left, v.Top, v.Width, v.Height)));
        }
        _blurred.Clip = clip;
        _marks.Source = RenderMarks();
        _selection.InvalidateVisual();
        PositionTextBox();
    }

    /// Annotations drawn by the export renderer onto a transparent overlay at device resolution.
    ImageSource? RenderMarks()
    {
        var marks = ShownAnnotations.Where(a => a.Id != _textEdit?.Id).ToList();
        if (marks.Count == 0) return null;
        double dpi = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1;
        int w = Math.Max(1, (int)Math.Round(_fitted.Width * dpi)), h = Math.Max(1, (int)Math.Round(_fitted.Height * dpi));
        using var bitmap = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Premul));
        bitmap.Erase(SKColors.Transparent);
        using (var canvas = new SKCanvas(bitmap)) Annotations.Draw(canvas, new SKSize(w, h), marks, PixelWidth);
        return Bitmaps.ToImageSource(bitmap);
    }

    /// Blur outlines (they are invisible on flat backgrounds), handles and the text frame.
    sealed class SelectionLayer(AnnotationCanvas owner) : FrameworkElement
    {
        protected override void OnRender(DrawingContext dc)
        {
            var accent = SystemColors.HighlightBrush;
            var size = owner.Fitted;
            var blur = owner.ShownBlur;
            for (int i = 0; i < blur.Count; i++)
            {
                bool selected = owner._selected is BlurItem b && b.Index == i;
                var pen = new Pen(accent, 1) { DashStyle = selected ? DashStyles.Solid : new DashStyle([5, 3], 0) };
                dc.PushOpacity(selected ? 1 : 0.6);
                dc.DrawRectangle(null, pen, ToRect(Geometry.Denormalize(blur[i], size)));
                dc.Pop();
                if (selected) Handle(dc, BottomRight(Geometry.Denormalize(blur[i], size)), round: false);
            }
            if (owner._selected is not AnnotationItem item || item.Id == owner._textEdit?.Id) return;
            switch (owner.ShownAnnotations.FirstOrDefault(a => a.Id == item.Id)?.Mark)
            {
                case RectangleMark r:
                    Handle(dc, BottomRight(Geometry.Denormalize(r.Rect, size)), round: false);
                    break;
                case ArrowMark a:
                    Handle(dc, new Point(a.From.X * size.Width, a.From.Y * size.Height), round: true);
                    Handle(dc, new Point(a.To.X * size.Width, a.To.Y * size.Height), round: true);
                    break;
                case TextMark t:
                    var f = Editing.TextFrame(t.Origin, t.Text, size, owner.PixelWidth);
                    dc.DrawRectangle(null, new Pen(accent, 1) { DashStyle = new DashStyle([4, 3], 0) },
                                     new Rect(f.Left - 3, f.Top - 3, f.Width + 6, f.Height + 6));
                    break;
            }
        }

        static Point BottomRight(SKRect r) => new(r.Right, r.Bottom);

        static Rect ToRect(SKRect r) => new(r.Left, r.Top, r.Width, r.Height);

        static void Handle(DrawingContext dc, Point center, bool round)
        {
            var pen = new Pen(SystemColors.HighlightBrush, 1.5);
            if (round) dc.DrawEllipse(Brushes.White, pen, center, 5, 5);
            else dc.DrawRectangle(Brushes.White, pen, new Rect(center.X - 5, center.Y - 5, 10, 10));
        }
    }

    // Mouse

    SKPoint ToSk(Point p) => new((float)p.X, (float)p.Y);

    SKPoint Normalized(Point p) =>
        new((float)Math.Clamp(p.X / _fitted.Width, 0, 1), (float)Math.Clamp(p.Y / _fitted.Height, 0, 1));

    CanvasHit? Hit(Point p, CanvasItem? selection) =>
        Editing.HitTest(ToSk(p), _blur, _annotations, selection, Fitted, PixelWidth,
                        includeBlurBodies: Tool is EditorTool.Select or EditorTool.Blur);

    void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_fitted.Width <= 0) return;
        if (_textEdit is not null) CommitText();
        _surface.Focus();
        _dragStart = e.GetPosition(_surface);
        _doubleClick = e.ClickCount >= 2;
        _workingBlur = null;
        _workingAnnotations = null;
        _mode = Begin(_dragStart);
        _surface.CaptureMouse();
        e.Handled = true;
    }

    void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_mode is null || e.LeftButton != MouseButtonState.Pressed) return;
        UpdateDrag(e.GetPosition(_surface));
        Redraw();
    }

    void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_mode is null) return;
        _surface.ReleaseMouseCapture();
        var p = e.GetPosition(_surface);
        bool wasIdle = _mode is Idle;
        double moved = Math.Abs(p.X - _dragStart.X) + Math.Abs(p.Y - _dragStart.Y);
        // A slightly shaky click with nothing to drag still counts as a click.
        if (moved < 3 || wasIdle) Tap(p);
        else Finish();
        _mode = null;
        _workingBlur = null;
        _workingAnnotations = null;
        Redraw();
        e.Handled = true;
    }

    DragMode Begin(Point p)
    {
        switch (Hit(p, _selected))
        {
            case { Item: BlurItem b, Handle: not null } when b.Index < _blur.Count:
                return new ResizeBlur(b.Index, _blur[b.Index]);
            case { Item: AnnotationItem a, Handle: { } handle }:
                var mark = _annotations.FirstOrDefault(x => x.Id == a.Id)?.Mark;
                if (mark is RectangleMark r && handle == CanvasHandle.Resize) return new ResizeBox(a.Id, r.Rect);
                return mark is ArrowMark arrow ? new MoveArrowEnd(a.Id, handle == CanvasHandle.ArrowStart, arrow) : new Idle();
            case { Item: BlurItem b } when b.Index < _blur.Count:
                Selected = b;
                return new MoveBlur(b.Index, _blur[b.Index]);
            case { Item: AnnotationItem a }:
                Selected = a;
                return _annotations.FirstOrDefault(x => x.Id == a.Id)?.Mark is { } m ? new MoveAnnotation(a.Id, m) : new Idle();
            default:
                Selected = null;
                return Tool is EditorTool.Blur or EditorTool.Rectangle or EditorTool.Arrow ? new Create(ToSk(p)) : new Idle();
        }
    }

    void UpdateDrag(Point p)
    {
        var d = new SKPoint((float)((p.X - _dragStart.X) / _fitted.Width), (float)((p.Y - _dragStart.Y) / _fitted.Height));
        var minSize = new SKSize((float)(MinSide / _fitted.Width), (float)(MinSide / _fitted.Height));
        switch (_mode)
        {
            case Create c:
                var a = Normalized(new Point(c.Start.X, c.Start.Y));
                var b = Normalized(p);
                var rect = new SKRect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
                if (Tool == EditorTool.Blur) _workingBlur = [.. _blur, rect];
                else if (Tool == EditorTool.Rectangle) _workingAnnotations = [.. _annotations, new Annotation(DraftId, new RectangleMark(rect))];
                else if (Tool == EditorTool.Arrow) _workingAnnotations = [.. _annotations, new Annotation(DraftId, new ArrowMark(a, b))];
                break;
            case MoveBlur m when m.Index < _blur.Count:
                var moved = ((RectangleMark)Editing.Translated(new RectangleMark(m.Original), d)).Rect;
                _workingBlur = _blur.Select((r, i) => i == m.Index ? moved : r).ToList();
                break;
            case ResizeBlur rb when rb.Index < _blur.Count:
                var resized = Editing.Resized(rb.Original, d, minSize);
                _workingBlur = _blur.Select((r, i) => i == rb.Index ? resized : r).ToList();
                break;
            case MoveAnnotation ma:
                var next = Editing.Translated(ma.Original, d);
                if (next is TextMark t) next = t with { Origin = Editing.ClampTextOrigin(t.Origin, t.Text, PixelSize) };
                _workingAnnotations = Replacing(ma.Id, next);
                break;
            case ResizeBox rbox:
                _workingAnnotations = Replacing(rbox.Id, new RectangleMark(Editing.Resized(rbox.Original, d, minSize)));
                break;
            case MoveArrowEnd end:
                var point = Normalized(p);
                _workingAnnotations = Replacing(end.Id, end.IsStart ? end.Original with { From = point } : end.Original with { To = point });
                break;
        }
    }

    void Finish()
    {
        switch (_mode)
        {
            case Create:
                if (Tool == EditorTool.Blur && _workingBlur is { Count: > 0 } blur)
                {
                    var v = Geometry.Denormalize(blur[^1], Fitted);
                    if (v.Width < MinSide || v.Height < MinSide) return;
                    BlurCommitted?.Invoke(blur);
                    Selected = new BlurItem(blur.Count - 1);
                }
                else if (_workingAnnotations is { Count: > 0 } marks && IsBigEnough(marks[^1].Mark))
                {
                    var real = new Annotation(Guid.NewGuid(), marks[^1].Mark);
                    AnnotationsCommitted?.Invoke([.. marks.Take(marks.Count - 1), real]);
                    Selected = new AnnotationItem(real.Id);
                }
                break;
            case MoveBlur or ResizeBlur:
                if (_workingBlur is { } nextBlur && !nextBlur.SequenceEqual(_blur)) BlurCommitted?.Invoke(nextBlur);
                break;
            case MoveAnnotation or ResizeBox or MoveArrowEnd:
                if (_workingAnnotations is { } nextMarks && !nextMarks.SequenceEqual(_annotations)) AnnotationsCommitted?.Invoke(nextMarks);
                break;
        }
    }

    bool IsBigEnough(Mark mark)
    {
        switch (mark)
        {
            case RectangleMark r:
                var v = Geometry.Denormalize(r.Rect, Fitted);
                return v.Width >= MinSide && v.Height >= MinSide;
            case ArrowMark a:
                double dx = (a.To.X - a.From.X) * _fitted.Width, dy = (a.To.Y - a.From.Y) * _fitted.Height;
                return dx * dx + dy * dy >= 64;
            default:
                return true;
        }
    }

    void Tap(Point p)
    {
        if (Hit(p, null) is { Item: AnnotationItem a }
            && _annotations.FirstOrDefault(x => x.Id == a.Id)?.Mark is TextMark t
            && (_doubleClick || Tool == EditorTool.Text))
        {
            BeginTextEdit(new TextEdit(a.Id, t.Origin), t.Text);
            return;
        }
        if (Tool == EditorTool.Text && _selected is null) BeginTextEdit(new TextEdit(null, Normalized(p)), "");
    }

    IReadOnlyList<Annotation> Replacing(Guid id, Mark mark) =>
        _annotations.Select(a => a.Id == id ? a with { Mark = mark } : a).ToList();

    void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Delete or Key.Back && _textEdit is null && _mode is null && _selected is not null)
        {
            DeleteSelected();
            e.Handled = true;
        }
    }

    // Text editing

    void BeginTextEdit(TextEdit edit, string text)
    {
        _textEdit = edit;
        _textBox.Text = text;
        _textBox.FontSize = Math.Max(1, AnnotationStyle.FontSize(PixelWidth) * _fitted.Width / PixelWidth);
        _textBox.FontFamily = new FontFamily("Microsoft YaHei UI");
        _textBox.Visibility = Visibility.Visible;
        Redraw();
        Dispatcher.BeginInvoke(() =>
        {
            _textBox.Focus();
            _textBox.SelectAll();
        });
    }

    void PositionTextBox()
    {
        if (_textEdit is null) return;
        Canvas.SetLeft(_textBox, _textEdit.Origin.X * _fitted.Width - 3);
        Canvas.SetTop(_textBox, _textEdit.Origin.Y * _fitted.Height);
    }

    void OnTextKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitText();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            EndTextEdit();
            _surface.Focus();
            e.Handled = true;
        }
    }

    /// Saves the text being typed, if any; empty text deletes an edited annotation.
    public void CommitText()
    {
        if (_textEdit is not { } edit) return;
        var text = _textBox.Text.Trim();
        EndTextEdit();
        var origin = Editing.ClampTextOrigin(edit.Origin, text, PixelSize);
        var next = _annotations.ToList();
        if (edit.Id is { } id)
        {
            int i = next.FindIndex(a => a.Id == id);
            if (i < 0) return;  // deleted meanwhile
            if (text.Length == 0)
            {
                next.RemoveAt(i);
                Selected = null;
            }
            else next[i] = next[i] with { Mark = new TextMark(origin, text) };
        }
        else if (text.Length > 0)
        {
            var created = new Annotation(Guid.NewGuid(), new TextMark(origin, text));
            next.Add(created);
            Selected = new AnnotationItem(created.Id);
        }
        if (!next.SequenceEqual(_annotations)) AnnotationsCommitted?.Invoke(next);
        _surface.Focus();
    }

    void EndTextEdit()
    {
        _textEdit = null;
        _textBox.Visibility = Visibility.Collapsed;
        Redraw();
    }

    static SolidColorBrush Frozen(Color c)
    {
        var brush = new SolidColorBrush(c);
        brush.Freeze();
        return brush;
    }
}
