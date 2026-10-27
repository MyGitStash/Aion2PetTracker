using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Aion2PetTracker.App;

/// <summary>Uploads only our own overlay bitmap to our own window. Transparent pixels have no backing
/// rectangle and are naturally click-through. Native graphics handles are released on every upload.</summary>
internal static class LayeredSurface
{
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X, Y; public NativePoint(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        public int Width, Height; public NativeSize(int w, int h)
        {
            Width = w;
            Height = h;
        }
    }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct Blend
    {
        public byte Operation, Flags, ConstantAlpha, AlphaFormat;
    }
    internal static void Present(IntPtr window, Bitmap bitmap, Point location, byte opacity)
    {
        IntPtr screen = GetDC(IntPtr.Zero), memory = IntPtr.Zero, image = IntPtr.Zero, previous = IntPtr.Zero;
        try
        {
            if (screen == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());
            memory = CreateCompatibleDC(screen);
            if (memory == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());
            image = bitmap.GetHbitmap(Color.FromArgb(0));
            previous = SelectObject(memory, image);
            if (previous == IntPtr.Zero || previous == new IntPtr(-1))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            var position = new NativePoint(location.X, location.Y);
            var origin = new NativePoint(0, 0);
            var size = new NativeSize(bitmap.Width, bitmap.Height);
            var blend = new Blend { ConstantAlpha = opacity, AlphaFormat = 1 };
            if (!UpdateLayeredWindow(window, screen, ref position, ref size, memory, ref origin, 0, ref blend, 2))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            if (previous != IntPtr.Zero && previous != new IntPtr(-1))
                SelectObject(memory, previous);
            if (image != IntPtr.Zero)
                DeleteObject(image);
            if (memory != IntPtr.Zero)
                DeleteDC(memory);
            if (screen != IntPtr.Zero)
                ReleaseDC(IntPtr.Zero, screen);
        }
    }
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr dst, ref NativePoint pos, ref NativeSize size, IntPtr src, ref NativePoint origin, uint key, ref Blend blend, uint flags);
}
