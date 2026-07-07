using System.Runtime.InteropServices;

namespace MouseShakeFinder;

/// <summary>
/// Global low-level mouse hook (WH_MOUSE_LL). Requires a message loop on
/// the installing thread — the WinForms Application.Run loop provides it.
/// </summary>
public sealed class MouseHook : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_MOUSEMOVE = 0x0200;

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    private delegate nint HookProc(int nCode, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookExW(int idHook, HookProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    // Field keeps the delegate alive; a local would be garbage-collected
    // while native code still calls it.
    private readonly HookProc _proc;
    private readonly nint _hook;

    public event Action<int, int, long>? MouseMoved;

    public MouseHook()
    {
        _proc = Callback;
        _hook = SetWindowsHookExW(WH_MOUSE_LL, _proc, 0, 0);
        if (_hook == 0)
        {
            throw new InvalidOperationException(
                $"Failed to install mouse hook (error {Marshal.GetLastWin32Error()})");
        }
    }

    private nint Callback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && wParam == WM_MOUSEMOVE)
        {
            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            MouseMoved?.Invoke(data.X, data.Y, Environment.TickCount64);
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose() => UnhookWindowsHookEx(_hook);
}
