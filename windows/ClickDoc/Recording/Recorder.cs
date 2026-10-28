using System.Threading.Channels;
using ClickDoc.Native;
using ClickDoc.Tray;
using SkiaSharp;

namespace ClickDoc.Recording;

/// Turns global clicks into steps. Each click is captured and inspected right away, in
/// parallel; appending is serialized so steps keep click order.
sealed class Recorder(AppState state, TrayIcon tray)
{
    static readonly TimeSpan TrayClickWindow = TimeSpan.FromSeconds(5);

    /// What a step's click hit, kept so a double-click can relabel it and a stop can trim taskbar clicks.
    sealed record StepInfo(DateTimeOffset At, bool IsShell, string? AppName, string? ElementName);

    readonly int _pid = Environment.ProcessId;
    readonly Dictionary<Guid, StepInfo> _info = [];
    MouseHook? _hook;
    Channel<RawClick>? _clicks;
    Task _reader = Task.CompletedTask;
    /// Every click waits for the one before it; a failing click is reported and skipped.
    readonly SerialQueue _queue = new(e => state.Dispatcher.BeginInvoke(() => state.LastError = L("截图失败：%@", e.Message)));
    ClickCounter _counter = new(500, 4, 4);
    int _doubleClickMs = 500;
    /// Read on the reader thread; follows the tray setting even mid-recording, like the Mac app.
    volatile CaptureScope _scope = state.Scope;
    // Where and when the most recent step's click happened (UI thread only).
    SKPointI? _lastPos;
    DateTimeOffset? _lastAt;

    public bool Start()
    {
        _doubleClickMs = (int)Win32.GetDoubleClickTime();
        _counter = new ClickCounter(_doubleClickMs, Win32.GetSystemMetrics(Win32.SM_CXDOUBLECLK), Win32.GetSystemMetrics(Win32.SM_CYDOUBLECLK));
        _scope = state.Scope;
        state.PropertyChanged -= OnStateChanged;
        state.PropertyChanged += OnStateChanged;
        var clicks = Channel.CreateUnbounded<RawClick>(new UnboundedChannelOptions { SingleReader = true });
        var hook = new MouseHook(click => clicks.Writer.TryWrite(click));
        if (!hook.Start())
        {
            state.LastError = L("无法监听鼠标点击");
            return false;
        }
        _hook = hook;
        _clicks = clicks;
        ForgetLastClick();
        _reader = Task.Run(async () =>
        {
            await foreach (var click in clicks.Reader.ReadAllAsync())
            {
                try { Receive(click); }
                catch (Exception e) { await state.Dispatcher.InvokeAsync(() => state.LastError = L("截图失败：%@", e.Message)); }
            }
        });
        return true;
    }

    /// `fromTray`: drops the trailing taskbar/overflow clicks it took to reach the tray icon.
    public async Task Stop(bool fromTray)
    {
        var stoppedAt = DateTimeOffset.Now;
        _hook?.Stop();
        _hook = null;
        _clicks?.Writer.TryComplete();
        await _reader;
        await _queue.Drain();
        if (fromTray) DropTrailingShellSteps(stoppedAt);
    }

    void OnStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppState.Scope)) _scope = state.Scope;
    }

    /// Forgets the previous click so it cannot be upgraded to a double-click (after edits that move steps).
    public void ForgetLastClick()
    {
        _lastPos = null;
        _lastAt = null;
    }

    /// Runs on the reader thread, in click order.
    void Receive(RawClick click)
    {
        int count = _counter.Register(click.Button, click.Pos, click.TickMs);
        var windows = WindowList.Snapshot();
        nint hit = WindowList.HitWindow(click.Pos);
        if (IsOwnUI(click.Pos, hit)) return;
        bool isShell = windows.FirstOrDefault(w => w.Handle == hit)?.IsShell ?? WindowList.IsShell(hit);
        // Start the screenshot now so it reflects this click, not later ones.
        bool needsCapture = !(click.Button == MouseButton.Left && count >= 3);
        var scope = _scope;
        Task<byte[]>? capture = needsCapture ? Task.Run(() => Capture(click.Pos, scope, windows, hit)) : null;
        var inspect = Task.Run(() => ElementInspector.Inspect(click.Pos, hit));
        _queue.Enqueue(() => Process(click, count, isShell, capture, inspect));
    }

    bool IsOwnUI(SKPointI p, nint hit) =>
        (hit != 0 && Win32.ProcessId(hit) == _pid) || tray.GetIconRect() is { } icon && icon.Contains(p.X, p.Y);

    async Task Process(RawClick click, int count, bool isShell, Task<byte[]>? capture,
                       Task<(string? AppName, string? ElementName)> inspect)
    {
        var names = await inspect;
        byte[]? png = null;
        Exception? error = null;
        if (capture is not null)
        {
            try { png = await capture; }
            catch (Exception e) { error = e; }
        }
        await state.Dispatcher.InvokeAsync(() => Apply(click, count, new StepInfo(click.At, isShell, names.AppName, names.ElementName), png, error));
    }

    /// Runs on the UI thread.
    void Apply(RawClick click, int count, StepInfo info, byte[]? png, Exception? error)
    {
        var tutorial = state.Tutorial;
        float scale = ScreenCapture.MonitorAt(click.Pos).Scale;
        bool canUpgrade = !tutorial.IsEmpty && Steps.CanUpgradeLastStep(_lastPos, _lastAt, click.Pos, click.At, _doubleClickMs, scale);
        var decision = Steps.Decide(click.Button, count, canUpgrade);
        switch (decision.Kind)
        {
            case StepDecisionKind.Ignore:
                return;
            case StepDecisionKind.UpgradeLastToDoubleClick:
                var last = tutorial.Steps[^1];
                _info.TryGetValue(last.Id, out var lastInfo);
                tutorial.UpdateLastStep(s => s with { Description = Naming.DescribeStep(ClickAction.DoubleClick, lastInfo?.AppName, lastInfo?.ElementName) });
                ForgetLastClick();
                state.Touch();
                return;
            case StepDecisionKind.NewStep:
                if (png is null)
                {
                    state.LastError = L("截图失败：%@", error?.Message ?? "");
                    return;
                }
                var step = new Step(Guid.NewGuid(), Naming.DescribeStep(decision.Action, info.AppName, info.ElementName), png, click.At);
                tutorial.Append(step);
                _info[step.Id] = info;
                _lastPos = click.Pos;
                _lastAt = click.At;
                state.Touch();
                return;
        }
    }

    /// Screenshot for one step: the monitor, cropped to the clicked window in window mode, with the highlight.
    static byte[] Capture(SKPointI click, CaptureScope scope, IReadOnlyList<WindowSnapshot> windows, nint hit)
    {
        var (image, monitor, scale) = ScreenCapture.CaptureMonitorAt(click);
        using (image)
        {
            var crop = scope == CaptureScope.Window ? Geometry.CaptureCropRect(click, windows, monitor, scale, hit) : null;
            // Menus opened by this very click appear after mouse-down: widen the crop to them.
            if (crop is { } first && Geometry.CaptureCropRect(click, WindowList.Snapshot(), monitor, scale) is { } wider)
            {
                var union = new SKRectI(Math.Min(first.Left, wider.Left), Math.Min(first.Top, wider.Top),
                                        Math.Max(first.Right, wider.Right), Math.Max(first.Bottom, wider.Bottom));
                crop = SKRectI.Intersect(union, monitor);
            }
            var origin = new SKPointI(monitor.Left, monitor.Top);
            using var cropped = new SKBitmap();
            var source = image;
            if (crop is { } c && image.ExtractSubset(cropped, Geometry.ToImagePixels(c, monitor)))
            {
                source = cropped;
                origin = new SKPointI(c.Left, c.Top);
            }
            using var marked = Imaging.RenderHighlight(source, Geometry.HighlightRect(click, origin, scale), scale);
            return Imaging.EncodePng(marked);
        }
    }

    void DropTrailingShellSteps(DateTimeOffset stoppedAt)
    {
        var tutorial = state.Tutorial;
        var tail = tutorial.Steps
            .Select(s => _info.TryGetValue(s.Id, out var i) ? (i.At, i.IsShell) : (s.CapturedAt, false))
            .ToList();
        int n = Geometry.TrailingShellSteps(tail, stoppedAt, TrayClickWindow);
        if (n == 0) return;
        tutorial.Remove(tutorial.Steps.TakeLast(n).Select(s => s.Id).ToHashSet());
        ForgetLastClick();
        state.Touch();
    }
}
