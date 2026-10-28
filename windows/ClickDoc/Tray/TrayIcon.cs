using System.Windows.Interop;
using ClickDoc.Native;
using SkiaSharp;

namespace ClickDoc.Tray;

/// The notification-area icon, driven directly through Shell_NotifyIcon so its on-screen
/// rectangle can be queried (clicks on it must not become steps).
sealed class TrayIcon : IDisposable
{
    const uint IconId = 1;
    const int CallbackMessage = Win32.WM_APP + 1;

    readonly Action _onClick;
    readonly HwndSource _window;
    readonly uint _taskbarCreated = Win32.RegisterWindowMessage("TaskbarCreated");
    readonly nint _idleIcon, _recordingIcon;
    bool _recording;
    string _tooltip = "ClickDoc";

    public TrayIcon(Action onClick)
    {
        _onClick = onClick;
        // A hidden top-level window rather than a message-only one: only top-level windows
        // receive the TaskbarCreated broadcast sent when Explorer restarts.
        _window = new HwndSource(new HwndSourceParameters("ClickDocTray") { WindowStyle = 0, Width = 0, Height = 0 });
        _window.AddHook(WndProc);
        _idleIcon = LoadIcon("tray-idle.ico");
        _recordingIcon = LoadIcon("tray-recording.ico");
        Update(Win32.NIM_ADD);
    }

    /// The hidden window that owns the icon; the tray menu brings it to the foreground.
    public nint Handle => _window.Handle;

    public void SetIcon(bool recording)
    {
        _recording = recording;
        Update(Win32.NIM_MODIFY);
    }

    public void SetTooltip(string text)
    {
        _tooltip = text;
        Update(Win32.NIM_MODIFY);
    }

    /// A notification from the tray icon (a toast on Windows 10/11).
    public unsafe void ShowBalloon(string title, string text)
    {
        var data = new NOTIFYICONDATAW
        {
            cbSize = (uint)sizeof(NOTIFYICONDATAW),
            hWnd = Handle,
            uID = IconId,
            uFlags = Win32.NIF_INFO,
            dwInfoFlags = Win32.NIIF_INFO,
        };
        for (int i = 0; i < Math.Min(title.Length, 63); i++) data.szInfoTitle[i] = title[i];
        for (int i = 0; i < Math.Min(text.Length, 255); i++) data.szInfo[i] = text[i];
        Win32.Shell_NotifyIcon(Win32.NIM_MODIFY, ref data);
    }

    /// The icon's screen rectangle in physical pixels, or null while it is hidden in the overflow area.
    public SKRectI? GetIconRect()
    {
        var id = new NOTIFYICONIDENTIFIER { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NOTIFYICONIDENTIFIER>(), hWnd = Handle, uID = IconId };
        return Win32.Shell_NotifyIconGetRect(ref id, out var r) == 0 ? new SKRectI(r.Left, r.Top, r.Right, r.Bottom) : null;
    }

    unsafe void Update(uint message)
    {
        var data = new NOTIFYICONDATAW
        {
            cbSize = (uint)sizeof(NOTIFYICONDATAW),
            hWnd = Handle,
            uID = IconId,
            uFlags = Win32.NIF_MESSAGE | Win32.NIF_ICON | Win32.NIF_TIP,
            uCallbackMessage = CallbackMessage,
            hIcon = _recording ? _recordingIcon : _idleIcon,
        };
        var tip = _tooltip.Length > 127 ? _tooltip[..127] : _tooltip;
        for (int i = 0; i < tip.Length; i++) data.szTip[i] = tip[i];
        Win32.Shell_NotifyIcon(message, ref data);
    }

    nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == CallbackMessage)
        {
            int mouse = (int)(lParam & 0xFFFF);
            if (mouse is Win32.WM_LBUTTONUP or Win32.WM_RBUTTONUP)
            {
                handled = true;
                _onClick();
            }
        }
        else if (msg == (int)_taskbarCreated)
        {
            Update(Win32.NIM_ADD);  // Explorer restarted and lost the icon
        }
        return 0;
    }

    static nint LoadIcon(string file) =>
        Win32.LoadImage(0, Path.Combine(AppContext.BaseDirectory, "Assets", file), Win32.IMAGE_ICON,
                        Win32.GetSystemMetrics(Win32.SM_CXSMICON), Win32.GetSystemMetrics(Win32.SM_CYSMICON), Win32.LR_LOADFROMFILE);

    public unsafe void Dispose()
    {
        var data = new NOTIFYICONDATAW { cbSize = (uint)sizeof(NOTIFYICONDATAW), hWnd = Handle, uID = IconId };
        Win32.Shell_NotifyIcon(Win32.NIM_DELETE, ref data);
        Win32.DestroyIcon(_idleIcon);
        Win32.DestroyIcon(_recordingIcon);
        _window.Dispose();
    }
}
