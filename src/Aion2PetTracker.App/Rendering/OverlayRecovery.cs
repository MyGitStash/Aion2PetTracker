using System.Runtime.InteropServices;

namespace Aion2PetTracker.App;

/// <summary>Maintains only our own windows. Restoring Z order never activates a window.
/// Pixel uploads happen on recovery, not on each timer tick.</summary>
internal sealed class OverlayRecovery
{
    private IntPtr _foreground;
    private long _nextRaise;
    private bool? _topmost;
    private bool _requested = true;

    internal void Request() => _requested = true;

    internal void Maintain(Form window, bool alwaysOnTop, Action redraw)
    {
        if (!window.Visible || !window.IsHandleCreated || window.IsDisposed)
            return;
        var foreground = GetForegroundWindow();
        bool minimized = IsIconic(window.Handle);
        bool requested = _requested;
        bool recover = requested || minimized || _topmost != alwaysOnTop || _foreground != foreground;
        long now = Environment.TickCount64;
        if (!recover && now < _nextRaise)
            return;
        _requested = false;
        _foreground = foreground;
        _nextRaise = now + 1000;
        if (minimized)
        {
            // WinForms can activate a visible minimized form while restoring its bounds.
            // Reset the managed state while hidden, then use ShowWithoutActivation.
            window.Hide();
            window.WindowState = FormWindowState.Normal;
            window.Show();
        }
        if (window.TopMost != alwaysOnTop)
            window.TopMost = alwaysOnTop;
        // Reassert native Z order even if WinForms still believes TopMost is set.
        // When disabled, do not repeatedly raise the overlay over other applications.
        if (alwaysOnTop || _topmost != alwaysOnTop || requested)
            SetWindowPos(window.Handle, new IntPtr(alwaysOnTop ? -1 : _topmost != alwaysOnTop ? -2 : 0), 0, 0, 0, 0,
                0x0001 | 0x0002 | 0x0010 | 0x0200); // NOSIZE | NOMOVE | NOACTIVATE | NOOWNERZORDER
        _topmost = alwaysOnTop;
        if (recover)
            redraw();
    }

    internal static bool IsRecoveryMessage(int message) => message is
        0x007E or // WM_DISPLAYCHANGE
        0x001A or // WM_SETTINGCHANGE (working area)
        0x02E0 or // WM_DPICHANGED
        0x031E;   // WM_DWMCOMPOSITIONCHANGED

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
}
