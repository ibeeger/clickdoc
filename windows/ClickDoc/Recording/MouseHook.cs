using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ClickDoc.Native;
using SkiaSharp;

namespace ClickDoc.Recording;

/// One mouse-down: `Pos` in physical screen pixels, `TickMs` for double-click timing.
readonly record struct RawClick(MouseButton Button, SKPointI Pos, DateTimeOffset At, long TickMs);

/// Listen-only global mouse-down hook on its own thread, so a busy UI thread can never make
/// Windows drop the hook. The callback only hands the click over and returns.
sealed unsafe class MouseHook(Action<RawClick> onClick) : IDisposable
{
    static MouseHook? _active;
    readonly Action<RawClick> _onClick = onClick;
    Thread? _thread;
    uint _threadId;

    public bool Start()
    {
        if (_thread is not null) return true;
        bool installed = false;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            _threadId = Win32.GetCurrentThreadId();
            _active = this;
            nint hook = Win32.SetWindowsHookEx(Win32.WH_MOUSE_LL, &HookProc, Win32.GetModuleHandle(null), 0);
            installed = hook != 0;
            ready.Set();
            if (!installed) return;
            while (Win32.GetMessage(out _, 0, 0, 0) > 0) { }
            Win32.UnhookWindowsHookEx(hook);
        }) { IsBackground = true, Name = "MouseHook" };
        thread.Start();
        ready.Wait();
        if (!installed)
        {
            _active = null;
            return false;
        }
        _thread = thread;
        return true;
    }

    public void Stop()
    {
        if (_thread is null) return;
        Win32.PostThreadMessage(_threadId, Win32.WM_QUIT, 0, 0);
        _thread.Join(TimeSpan.FromSeconds(1));
        _thread = null;
        _active = null;
    }

    public void Dispose() => Stop();

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    static nint HookProc(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && (wParam == Win32.WM_LBUTTONDOWN || wParam == Win32.WM_RBUTTONDOWN) && _active is { } hook)
        {
            try
            {
                var data = (MSLLHOOKSTRUCT*)lParam;
                hook._onClick(new RawClick(wParam == Win32.WM_RBUTTONDOWN ? MouseButton.Right : MouseButton.Left,
                                          new SKPointI(data->pt.X, data->pt.Y), DateTimeOffset.Now, Environment.TickCount64));
            }
            catch (Exception) { /* an exception here would take the process down */ }
        }
        return Win32.CallNextHookEx(0, nCode, wParam, lParam);
    }
}
