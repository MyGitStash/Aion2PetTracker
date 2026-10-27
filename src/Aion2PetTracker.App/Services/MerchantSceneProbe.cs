using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Aion2PetTracker.Core.Capture;
using Aion2PetTracker.Core.Pets;
namespace Aion2PetTracker.App;

internal sealed record MerchantScene(bool GameFocused, bool ShopVisible, Rectangle Bounds, double Score);
/// <summary>Local foreground-menu check, at most once/second and only while offers exist.
/// Reads a small header rectangle, never stores pixels, never reads game process memory.</summary>
internal sealed class MerchantSceneProbe
{
    private readonly HeaderSignature _signature;
    private Task<MerchantScene>? _pending;
    private long _next;
    private MerchantScene _latest = new(false, false, Rectangle.Empty, 0);
    public MerchantSceneProbe()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("shop-header-signature.json")!;
        _signature = JsonSerializer.Deserialize<HeaderSignature>(stream)!;
    }
    public MerchantScene Update(bool enabled, long now)
    {
        if (_pending?.IsCompleted == true)
        {
            _latest = _pending.IsCompletedSuccessfully ? _pending.Result : new(false, false, Rectangle.Empty, 0);
            _pending = null;
        }
        if (!enabled)
            return new(false, false, Rectangle.Empty, 0);
        if (_pending is null && now >= _next)
        {
            _next = now + 1000;
            _pending = Task.Run(Read);
        }
        return _latest;
    }
    private MerchantScene Read()
    {
        var hwnd = GetForegroundWindow();
        GetWindowThreadProcessId(hwnd, out uint pid);
        if (!GameProcessLocator.FindGameProcessIds().Contains((int)pid) || !GetClientRect(hwnd, out var rect) || rect.Right < 640 || rect.Bottom < 360)
            return new(false, false, Rectangle.Empty, 0);
        var origin = new NativePoint();
        if (!ClientToScreen(hwnd, ref origin))
            return new(false, false, Rectangle.Empty, 0);
        var bounds = new Rectangle(origin.X, origin.Y, rect.Right, rect.Bottom);
        var scales = new[] { bounds.Width / 1920f, bounds.Height / 1080f, 1f, 1.25f, 1.5f, 2f }.Where(k => k >= .5f && k <= 3).Distinct().ToArray();
        float max = scales.Max();
        int width = Math.Min(bounds.Width, (int)Math.Ceiling(332 * max) + 2), height = Math.Min(bounds.Height, (int)Math.Ceiling(66 * max) + 2);
        try
        {
            using var bitmap = new Bitmap(width, height);
            using (var g = Graphics.FromImage(bitmap))
                g.CopyFromScreen(bounds.Location, Point.Empty, bitmap.Size, CopyPixelOperation.SourceCopy);
            double score = 0;
            foreach (float scale in scales)
                score = Math.Max(score, ShopHeaderMatcher.Score(_signature, (x, y) => { int px = (int)Math.Round(x * scale), py = (int)Math.Round(y * scale); if (px >= width || py >= height) return 0; var c = bitmap.GetPixel(px, py); return (c.R * 299 + c.G * 587 + c.B * 114) / 1000; }));
            return new(true, score >= .78, bounds, score);
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or ArgumentException or ExternalException) { return new(true, false, bounds, 0); }
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
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hwnd, ref NativePoint point);
}
