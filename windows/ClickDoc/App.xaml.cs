using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ClickDoc.Editor;
using ClickDoc.Native;
using ClickDoc.Recording;
using ClickDoc.Tray;

namespace ClickDoc;

public partial class App : Application
{
    const string InstanceMutexName = @"Local\ClickDoc.SingleInstance";
    const string ShowEditorEventName = @"Local\ClickDoc.ShowEditor";

    Mutex? _instance;
    EventWaitHandle? _showEditorSignal;
    AppState? _state;
    TrayIcon? _tray;
    EditorWindow? _editor;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (Environment.OSVersion.Version.Build < 19041)
        {
            MessageBox.Show(L("ClickDoc 需要 Windows 10 2004 或更高版本。"), "ClickDoc", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }
        _instance = new Mutex(true, InstanceMutexName, out bool first);
        if (!first)
        {
            SignalRunningInstance();
            Shutdown();
            return;
        }
        _showEditorSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEditorEventName);
        new Thread(() =>
        {
            while (_showEditorSignal.WaitOne()) Dispatcher.BeginInvoke(ShowEditor);
        }) { IsBackground = true, Name = "ShowEditorSignal" }.Start();

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        CaptureExclusion.Install();
        _state = new AppState(Dispatcher);
        _tray = new TrayIcon(ShowTrayMenu);
        _state.Recorder = new Recorder(_state, _tray);
        _state.ShowEditorRequested += ShowEditor;
        _state.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(AppState.IsRecording)) return;
            _tray.SetIcon(_state.IsRecording);
            _tray.SetTooltip(_state.IsRecording ? L("ClickDoc（录制中）") : "ClickDoc");
        };
        SessionEnding += (_, _) => _state.FlushSave();
        ShowEditor();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _state?.FlushSave();
        _tray?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }

    /// A second launch brings the running instance's editor forward instead of starting another app.
    static void SignalRunningInstance()
    {
        try
        {
            Win32.AllowSetForegroundWindow(Win32.ASFW_ANY);
            using var signal = EventWaitHandle.OpenExisting(ShowEditorEventName);
            signal.Set();
        }
        catch (WaitHandleCannotBeOpenedException) { /* the other instance is just exiting */ }
    }

    void ShowEditor()
    {
        if (_state is null) return;
        _editor ??= new EditorWindow(_state);
        _editor.Show();
        if (_editor.WindowState == WindowState.Minimized) _editor.WindowState = WindowState.Normal;
        _editor.Activate();
    }

    void ShowTrayMenu()
    {
        if (_state is null || _tray is null) return;
        var state = _state;
        var menu = new ContextMenu { Placement = PlacementMode.MousePoint };
        if (state.LastError is { } error)
        {
            menu.Items.Add(Item(error, null, enabled: false));
            menu.Items.Add(new Separator());
        }
        menu.Items.Add(state.IsRecording
            ? Item(L("停止录制"), () => state.StopRecording(fromTray: true))
            : Item(L("开始录制"), state.StartRecording));
        var scope = Item(L("截图范围"), null);
        foreach (var (value, title) in new[] { (CaptureScope.Window, L("当前窗口")), (CaptureScope.Screen, L("整个屏幕")) })
        {
            var option = Item(title, () => state.Scope = value);
            option.IsCheckable = true;
            option.IsChecked = state.Scope == value;
            scope.Items.Add(option);
        }
        menu.Items.Add(scope);
        menu.Items.Add(Item(L("打开编辑器"), ShowEditor));
        menu.Items.Add(Item(L("新建教程"), state.NewTutorial));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item(L("退出"), Quit));
        // The menu only closes on an outside click when our process owns the foreground window.
        Win32.SetForegroundWindow(_tray.Handle);
        menu.IsOpen = true;
    }

    /// A menu item whose header is plain text, so underscores in paths are not taken as access keys.
    static MenuItem Item(string text, Action? onClick, bool enabled = true)
    {
        var item = new MenuItem { Header = new TextBlock { Text = text }, IsEnabled = enabled };
        if (onClick is not null) item.Click += (_, _) => onClick();
        return item;
    }

    void Quit()
    {
        _state?.StopRecording();
        _editor?.AllowClose();
        Shutdown();
    }
}
