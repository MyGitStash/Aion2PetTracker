using System.Reflection;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Text.Json;
using Aion2PetTracker.Core.Pets;

namespace Aion2PetTracker.App;

internal sealed class PetSoulBoard : UiControl
{
    public PetSoulRow? Row;
    public bool Demo;
    private readonly Dictionary<int, Bitmap> _portraits = [];
    private Bitmap? Portrait(int id)
    {
        if (_portraits.TryGetValue(id, out var image))
            return image;
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"peticon/{id}.png");
        if (stream is null)
            return null;
        using var decoded = Image.FromStream(stream);
        if (_portraits.Count >= 16)
        {
            var oldest = _portraits.First();
            oldest.Value.Dispose();
            _portraits.Remove(oldest.Key);
        }
        _portraits[id] = image = new Bitmap(decoded);
        return image;
    }
    protected override void Draw(Graphics g) => DrawOverlay(g, W, H);
    internal void DrawOverlay(Graphics g, float W, float H)
    {
        if (Row is not { } r)
            return;
        var gold = Color.FromArgb(218, 166, 79);
        var cream = Color.FromArgb(255, 235, 193);
        var area = new RectangleF(1, 3, W - 2, H - 6);
        using (var shape = Theme.Round(area, 13))
        using (var fill = new LinearGradientBrush(area, Color.FromArgb(21, 43, 67), Color.FromArgb(5, 12, 23), 90f))
            g.FillPath(fill, shape);
        Theme.Stroke(g, area, Color.FromArgb(103, 66, 25), 13, 2.8f);
        Theme.Stroke(g, new(2, 4, W - 4, H - 8), gold, 12, 1.2f);
        Theme.Stroke(g, new(4, 6, W - 8, H - 12), Color.FromArgb(83, 124, 158), 11, .7f);
        using (var shine = new Pen(Color.FromArgb(65, 158, 196, 224), .7f))
            g.DrawLine(shine, 77, 10, W - 18, 10);
        // Static curves and ornament; repaint only when a real value changes.
        var oldClip = g.Save();
        using (var clip = Theme.Round(new(5, 7, W - 10, H - 14), 10))
            g.SetClip(clip);
        using (var line = new Pen(Color.FromArgb(23, 92, 154, 216), 5))
            g.DrawBezier(line, 53, H - 5, W * .5f, -30, W * .6f, H + 10, W - 9, 15);
        g.Restore(oldClip);
        var portrait = new RectangleF(9, 7, 62, 62);
        using (var bg = new SolidBrush(Color.FromArgb(4, 14, 24)))
            g.FillEllipse(bg, portrait);
        using (var halo = new Pen(Color.FromArgb(65, 215, 157, 65), 5))
            g.DrawEllipse(halo, portrait);
        using (var ring = new Pen(cream, 1.3f))
            g.DrawEllipse(ring, portrait);
        if (Portrait(r.PetId) is { } image)
        {
            var saved = g.Save();
            using var circle = new GraphicsPath();
            circle.AddEllipse(12, 10, 56, 56);
            g.SetClip(circle);
            g.DrawImage(image, new Rectangle(12, 10, 56, 56));
            g.Restore(saved);
        }
        else
            Theme.TextAt(g, "?", Theme.Font(18, true), Theme.Muted, portrait, StringAlignment.Center);
        void Jewel(float x, float y, float size)
        {
            PointF[] points = [new(x, y - size), new(x + size * .48f, y), new(x, y + size), new(x - size * .48f, y)];
            using var b = new LinearGradientBrush(new RectangleF(x - size, y - size, size * 2, size * 2), cream, gold, 60f);
            g.FillPolygon(b, points);
            using var p = new Pen(gold, .6f);
            g.DrawPolygon(p, points);
        }
        Jewel(40, 8, 6);
        Jewel(40, 68, 5);
        Jewel(9, 38, 4);
        Jewel(71, 38, 4);
        Jewel(W * .5f, 4, 3);
        Jewel(W * .5f, H - 4, 3);
        Theme.TextAt(g, r.Name, Theme.Font(11, true), cream, new(81, 10, W - 133, 24));
        var badge = new RectangleF(W - 47, 11, 36, 24);
        Theme.Fill(g, badge, Color.FromArgb(11, 24, 38), 6);
        Theme.Stroke(g, badge, gold, 6);
        Theme.TextAt(g, r.Notification ?? $"+{r.RecentGain}", Theme.Font(r.Notification is null ? 11 : 8.5f, true), Color.FromArgb(255, 215, 108), badge, StringAlignment.Center);
        var group = r.Genus + (Demo ? " · PREVIEW" : r.Progress is null ? $" · +{r.SessionGain} this session" : "");
        Theme.TextAt(g, group, Theme.Font(8), Color.FromArgb(162, 185, 213), new(82, 35, 126, 19));
        Theme.TextAt(g, r.Progress is { } progressLabel ? (r.Uncertain ? "≈ " : "") + progressLabel.Label : "Total unknown", Theme.Font(9, true), r.Progress is null ? Theme.Muted : cream, new(207, 34, W - 219, 21), StringAlignment.Far);
        var bar = new RectangleF(81, 59, W - 94, 9);
        Theme.Fill(g, bar, Color.FromArgb(4, 10, 18), 5);
        Theme.Stroke(g, bar, gold, 5, .7f);
        Theme.Fill(g, new(bar.X + 3, bar.Y + 3, bar.Width - 6, 3), Color.FromArgb(33, 49, 68), 2);
        if (r.Progress is { } p)
        {
            float ratio = p.Level == 3 ? 1 : Math.Clamp((float)p.Current / p.Goal, 0, 1);
            if (ratio > 0)
            {
                var progress = new RectangleF(bar.X + 2, bar.Y + 2, Math.Max(1, (bar.Width - 4) * ratio), 5);
                using var path = Theme.Round(progress, 3);
                using var goldFill = new LinearGradientBrush(progress, Color.FromArgb(255, 245, 150), Color.FromArgb(237, 147, 31), 90f);
                g.FillPath(goldFill, path);
            }
        }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var b in _portraits.Values)
                b.Dispose();
            _portraits.Clear();
        }
        base.Dispose(disposing);
    }
}

internal sealed class PetSoulsOverlay : Form
{
    private readonly TrackerPreferences _prefs;
    private readonly Action _save;
    private readonly SoulCurrencyHeader _header = new();
    private readonly Dictionary<int, PetSoulBoard> _boards = [];
    private readonly List<(PetSoulBoard Board, RectangleF Bounds)> _cards = [];
    private readonly ToolTip _tip = new();
    private Bitmap? _surface;
    private RectangleF _headerBounds;
    private float _scale;
    private byte _alpha;
    private bool _handleDirty;
    private bool _locked;
    private Point _anchor;
    public PetSoulsOverlay(TrackerPreferences prefs, Action save)
    {
        _prefs = prefs;
        _save = save;
        Text = "Aion2 Pet Tracker · Souls";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        // One premultiplied-alpha surface. No rectangular child-window background or color key.
        AutoScaleMode = AutoScaleMode.None;
        MouseDown += Drag;
        MouseMove += (_, e) =>
        {
            if (_scale <= 0)
                return;
            var p = new PointF(e.X / _scale, e.Y / _scale);
            string text = _headerBounds.Contains(p) ? _header.TooltipAt(p.X - _headerBounds.X, _headerBounds.Width) : "";
            if (_tip.GetToolTip(this) != text)
                _tip.SetToolTip(this, text);
        };
        var a = Screen.PrimaryScreen!.WorkingArea;
        Location = new(prefs.PetSoulsX ?? a.Left + 30, prefs.PetSoulsY ?? a.Bottom - 150);
        _anchor = Location;
    }
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get
        {
            var p = base.CreateParams;
            p.ExStyle |= 0x08000000 | 0x80 | 0x80000;
            if (_prefs?.PetSoulsLocked == true)
                p.ExStyle |= 0x20;
            return p;
        }
    }
    public void RefreshState(PetSoulView? view, bool show, bool demo = false, SoulCurrencyView? currencies = null)
        => RefreshCore(view, show, demo, currencies);
    private void RefreshCore(PetSoulView? view, bool show, bool demo, SoulCurrencyView? currencies, float? previewScale = null)
    {
        if (_locked != _prefs.PetSoulsLocked)
        {
            _locked = _prefs.PetSoulsLocked;
            if (IsHandleCreated)
                RecreateHandle();
        }
        var rows = view?.Rows ?? [];
        if (!show || !_prefs.PetSouls)
        {
            if (Visible)
                Hide();
            return;
        }
        var a = Screen.FromControl(this).WorkingArea;
        float k = previewScale ?? DeviceDpi / 96f;
        const int headerSpace = 48, rowPitch = 84;
        int maxRows = Math.Max(1, (int)(a.Height / k - headerSpace + 8) / rowPitch);
        int maxColumns = Math.Max(1, (int)(a.Width / k + 8) / 368);
        int columns = Math.Max(1, Math.Min(maxColumns, (rows.Count + maxRows - 1) / maxRows));
        int perColumn = Math.Min(maxRows, (rows.Count + columns - 1) / columns);
        var visible = rows.Take(perColumn * columns).ToList();
        bool changed = _scale != k || _handleDirty;
        foreach (int id in _boards.Keys.Where(id => !visible.Any(r => r.PetId == id)).ToArray())
        {
            _boards[id].Dispose();
            _boards.Remove(id);
            changed = true;
        }
        float cardGridWidth = columns * 368 - 8, width = Math.Max(420, cardGridWidth);
        var size = new Size((int)Math.Round(width * k), (int)Math.Round((perColumn == 0 ? 42 : headerSpace + perColumn * rowPitch - 8) * k));
        changed |= Size != size;
        Size = size;
        var currencyView = currencies ?? SoulCurrencyView.Unknown;
        bool walletChanged = _header.Demo != demo || _header.View.Cached != currencyView.Cached || _header.View.Uncertain != currencyView.Uncertain || !_header.View.Balances.SequenceEqual(currencyView.Balances);
        _header.View = currencyView;
        _header.Demo = demo;
        _headerBounds = new((width - 420) / 2, 0, 420, 42);
        changed |= walletChanged;
        var previousBounds = _cards.Select(c => c.Bounds).ToArray();
        _cards.Clear();
        for (int i = 0; i < visible.Count; i++)
        {
            var row = visible[i];
            if (!_boards.TryGetValue(row.PetId, out var board))
            {
                board = new PetSoulBoard();
                _boards.Add(row.PetId, board);
            }
            changed |= board.Row != row || board.Demo != demo;
            board.Row = row;
            board.Demo = demo;
            _cards.Add((board, new((width - cardGridWidth) / 2 + i / perColumn * 368, headerSpace + i % perColumn * rowPitch, 360, 76)));
        }
        changed |= !previousBounds.SequenceEqual(_cards.Select(c => c.Bounds));
        Location = new(Math.Clamp(_anchor.X, a.Left, Math.Max(a.Left, a.Right - Width)), Math.Clamp(_anchor.Y, a.Top, Math.Max(a.Top, a.Bottom - Height)));
        var alpha = (byte)Math.Clamp((int)Math.Round(_prefs.OverlayOpacity * 2.55), 0, 255);
        changed |= _alpha != alpha;
        _alpha = alpha;
        _scale = k;
        if (changed || _surface is null)
        {
            var surface = ComposeSurface();
            var previous = _surface;
            _surface = surface;
            previous?.Dispose();
        }
        if (previewScale is not null)
            return;
        if (!Visible)
        {
            Show();
            changed = true;
        }
        if (changed || _handleDirty)
        {
            Present();
            _handleDirty = false;
        }
    }
    private Bitmap ComposeSurface()
    {
        var image = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(image);
        g.Clear(Color.Transparent);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        // Grayscale antialiasing also works on translucent pixels; ClearType assumes an opaque background.
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.ScaleTransform(_scale, _scale);
        void DrawAt(RectangleF bounds, Action<Graphics, float, float> draw)
        {
            var state = g.Save();
            g.TranslateTransform(bounds.X, bounds.Y);
            g.SetClip(new RectangleF(0, 0, bounds.Width, bounds.Height));
            draw(g, bounds.Width, bounds.Height);
            g.Restore(state);
        }
        DrawAt(_headerBounds, _header.DrawOverlay);
        foreach (var card in _cards)
            DrawAt(card.Bounds, card.Board.DrawOverlay);
        return image;
    }
    public void RenderPreview(string path, PetSoulView view, SoulCurrencyView? currencies = null, float scale = 1)
    {
        RefreshCore(view, true, true, currencies, scale);
        _surface!.Save(path, ImageFormat.Png);
        int softEdgePixels = 0;
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                int alpha = _surface.GetPixel(x, y).A;
                if (alpha is > 0 and < 255)
                    softEdgePixels++;
            }
        int[] corners = [_surface.GetPixel(0, 0).A, _surface.GetPixel(Width - 1, 0).A, _surface.GetPixel(0, Height - 1).A, _surface.GetPixel(Width - 1, Height - 1).A];
        bool clearGap = _cards.Count == 0 || _surface.GetPixel(Width / 2, (int)(44 * scale)).A == 0;
        bool clearSides = _cards.Count == 0 || _surface.GetPixel(0, (int)(64 * scale)).A == 0;
        bool centered = _cards.Count == 0 || _cards.Max(c => c.Bounds.Right) + _cards.Min(c => c.Bounds.Left) == _headerBounds.Left + _headerBounds.Right;
        if (corners.Any(a => a != 0) || !clearGap || !clearSides || !centered || softEdgePixels == 0)
            throw new InvalidOperationException("Soul overlay transparency/layout check failed");
        // Exercise the actual Windows alpha upload as well, with zero opacity and no focus activation.
        _alpha = 0;
        Show();
        Present();
        Hide();
        File.WriteAllText(Path.ChangeExtension(path, "checks.json"), JsonSerializer.Serialize(new
        {
            scale,
            width = Width,
            height = Height,
            corners,
            clearGap,
            clearSides,
            centered,
            softEdgePixels,
            nativeAlphaUpload = true
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
    protected override void OnPaintBackground(PaintEventArgs e)
    {
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        if (_surface is not null)
            Present();
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _handleDirty = true;
    }
    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        _handleDirty = true;
    }
    private void Present()
    {
        if (_surface is not null && IsHandleCreated)
            LayeredSurface.Present(Handle, _surface, Location, _alpha);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _surface?.Dispose();
            _header.Dispose();
            _tip.Dispose();
            foreach (var board in _boards.Values)
                board.Dispose();
        }
        base.Dispose(disposing);
    }
    private void Drag(object? sender, MouseEventArgs e)
    {
        if (_prefs.PetSoulsLocked || e.Button != MouseButtons.Left)
            return;
        ReleaseCapture();
        SendMessage(Handle, 0xA1, new IntPtr(2), IntPtr.Zero);
        _anchor = Location;
        _prefs.PetSoulsX = Left;
        _prefs.PetSoulsY = Top;
        _save();
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x21)
        {
            m.Result = new IntPtr(3);
            return;
        }
        base.WndProc(ref m);
    }
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hwnd, uint msg, IntPtr w, IntPtr l);
}
