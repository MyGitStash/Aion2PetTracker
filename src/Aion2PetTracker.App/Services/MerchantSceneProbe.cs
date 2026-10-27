using System.Buffers;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Aion2PetTracker.Core.Capture;
using Aion2PetTracker.Core.Pets;
namespace Aion2PetTracker.App;

internal sealed record MerchantScene(bool GameFocused, bool ShopVisible, Rectangle Bounds, double Score,
    double Scale = 0, int OffsetX = 0, int OffsetY = 0);

/// <summary>Checks only bounded header regions, at most once a second. Manual mode
/// reads foreground/window metadata only. No screenshots are saved or uploaded.</summary>
internal sealed class MerchantSceneProbe
{
    private sealed record Reading(IntPtr Window, bool Manual, MerchantScene Scene, uint ProcessId = 0);
    private static MerchantScene Empty => new(false, false, Rectangle.Empty, 0);
    private readonly HeaderSignature _signature;
    private Task<Reading>? _pending;
    private Reading? _latest;
    private Reading? _lastGame;
    private IntPtr _observedForeground;
    private long _next;
    private bool _manual;

    public MerchantSceneProbe()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("shop-header-signature.json")!;
        _signature = JsonSerializer.Deserialize<HeaderSignature>(stream)!;
    }

    public MerchantScene Update(bool enabled, long now, bool manual = false)
    {
        if (_manual != manual)
        {
            _manual = manual;
            _latest = null;
            _lastGame = null;
            _next = 0;
        }
        if (_pending?.IsCompleted == true)
        {
            _latest = _pending.IsCompletedSuccessfully && _pending.Result.Manual == manual ? _pending.Result : null;
            if (_latest?.Scene.GameFocused == true)
                _lastGame = _latest;
            _pending = null;
        }
        if (!enabled)
        {
            _latest = null;
            _lastGame = null;
            _next = 0;
            return Empty;
        }
        var foreground = GetForegroundWindow();
        if (_observedForeground != foreground)
        {
            _observedForeground = foreground;
            _next = 0;
        }
        if (_pending is null && now >= _next)
        {
            _next = now + 1000;
            _pending = Task.Run(() => Read(manual));
        }
        if (_latest is { } latest && latest.Window == foreground && latest.Scene.GameFocused)
            return latest.Scene;
        // Retain the last observed game scene across Alt-Tab; never sample another app's pixels.
        // The caller decides whether background display is enabled. A closed game invalidates it.
        if (_lastGame is { } last && last.Window != foreground && IsWindow(last.Window))
        {
            GetWindowThreadProcessId(last.Window, out uint pid);
            if (pid == last.ProcessId)
                return last.Scene with { GameFocused = false };
        }
        return Empty;
    }

    private Reading Read(bool manual)
    {
        var hwnd = GetForegroundWindow();
        GetWindowThreadProcessId(hwnd, out uint pid);
        if (!GameProcessLocator.FindGameProcessIds().Contains((int)pid) || !GetClientRect(hwnd, out var rect) ||
            rect.Right < 640 || rect.Bottom < 360)
            return new(hwnd, manual, Empty);
        var origin = new NativePoint();
        if (!ClientToScreen(hwnd, ref origin))
            return new(hwnd, manual, Empty);
        var bounds = new Rectangle(origin.X, origin.Y, rect.Right, rect.Bottom);
        if (manual)
            return new(hwnd, true, new(true, true, bounds, 0), pid);
        double[] scales = [bounds.Width / 1920d, bounds.Height / 1080d, 1, 1.25, 1.5, 2];
        // A letterboxed client can anchor its UI inside the centred 16:9 viewport.
        Point[] anchors = [Point.Empty, new(Math.Max(0, (int)Math.Round((bounds.Width - bounds.Height * 16d / 9) / 2)),
            Math.Max(0, (int)Math.Round((bounds.Height - bounds.Width * 9d / 16) / 2)))];
        var best = new HeaderMatch(0, 1, 0, 0);
        try
        {
            foreach (var anchor in anchors.Distinct())
            {
                int width = Math.Min(bounds.Width - anchor.X, 332 * 3 + 16);
                int height = Math.Min(bounds.Height - anchor.Y, 66 * 3 + 16);
                if (width <= 0 || height <= 0)
                    continue;
                using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppRgb);
                using (var g = Graphics.FromImage(bitmap))
                    g.CopyFromScreen(new Point(bounds.X + anchor.X, bounds.Y + anchor.Y), Point.Empty, bitmap.Size, CopyPixelOperation.SourceCopy);
                var pixels = ArrayPool<byte>.Shared.Rent(width * height);
                HeaderMatch match;
                try
                {
                    Luminance(bitmap, pixels.AsSpan(0, width * height));
                    match = ShopHeaderSearch.Find(_signature, pixels.AsSpan(0, width * height), width, height, scales);
                }
                finally { ArrayPool<byte>.Shared.Return(pixels, clearArray: true); }
                if (match.Score > best.Score)
                    best = match with
                    {
                        OffsetX = match.OffsetX + anchor.X,
                        OffsetY = match.OffsetY + anchor.Y
                    };
                if (best.Score >= .78)
                    break;
            }
            return GetForegroundWindow() != hwnd ? new(hwnd, false, Empty) :
                new(hwnd, false, new(true, best.Score >= .78, bounds, best.Score, best.Scale, best.OffsetX, best.OffsetY), pid);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or ArgumentException or ExternalException)
        {
            return new(hwnd, false, new(true, false, bounds, 0), pid);
        }
    }

    private static void Luminance(Bitmap bitmap, Span<byte> result)
    {
        var data = bitmap.LockBits(new(Point.Empty, bitmap.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb);
        try
        {
            byte[] row = new byte[Math.Abs(data.Stride)];
            for (int y = 0; y < bitmap.Height; y++)
            {
                Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, row.Length);
                for (int x = 0; x < bitmap.Width; x++)
                    result[y * bitmap.Width + x] = (byte)((row[x * 4 + 2] * 299 + row[x * 4 + 1] * 587 + row[x * 4] * 114) / 1000);
            }
        }
        finally { bitmap.UnlockBits(data); }
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X, Y;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
    }
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hwnd, ref NativePoint point);
}
