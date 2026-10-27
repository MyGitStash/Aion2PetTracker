using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Aion2PetTracker.Core.Storage;

namespace Aion2PetTracker.App;

internal static class Theme
{
    public static readonly Color Bg = Color.FromArgb(9, 12, 20), Sidebar = Color.FromArgb(12, 16, 27), Card = Color.FromArgb(18, 24, 37),
        Card2 = Color.FromArgb(26, 34, 51), Hover = Color.FromArgb(36, 48, 70), Line = Color.FromArgb(38, 50, 72),
        Text = Color.FromArgb(234, 239, 248), Muted = Color.FromArgb(136, 150, 176), Accent = Color.FromArgb(62, 224, 190),
        Accent2 = Color.FromArgb(70, 160, 255), Amber = Color.FromArgb(255, 196, 94), Danger = Color.FromArgb(255, 107, 120);

    private static readonly string Family = PickFamily();
    private static readonly Dictionary<(int, bool), Font> Fonts = [];
    private static string PickFamily()
    {
        try
        {
            using var fc = new InstalledFontCollection();
            return fc.Families.Any(f => f.Name == "Segoe UI Variable Text") ? "Segoe UI Variable Text" : "Segoe UI";
        }
        catch { return "Segoe UI"; }
    }
    // Fonts are cached for the process lifetime: never dispose them.
    public static Font Font(float size = 10, bool bold = false)
    {
        var key = ((int)(size * 10), bold);
        if (!Fonts.TryGetValue(key, out var f))
            Fonts[key] = f = new Font(Family, size, bold ? FontStyle.Bold : FontStyle.Regular);
        return f;
    }
    public static GraphicsPath Round(RectangleF r, float radius = 14)
    {
        var p = new GraphicsPath();
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0.5f)
        {
            p.AddRectangle(r);
            return p;
        }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
    public static void Fill(Graphics g, RectangleF rect, Color color, float radius = 14)
    {
        using var path = Round(rect, radius);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
    }
    public static void Stroke(Graphics g, RectangleF rect, Color color, float radius = 14, float width = 1)
    {
        using var path = Round(rect, radius);
        using var pen = new Pen(color, width);
        g.DrawPath(pen, path);
    }

    private static StringFormat Format(StringAlignment a) => new()
    {
        Alignment = a,
        LineAlignment = StringAlignment.Center,
        Trimming = StringTrimming.EllipsisCharacter,
        FormatFlags = StringFormatFlags.NoWrap
    };
    private static readonly StringFormat FNear = Format(StringAlignment.Near), FCenter = Format(StringAlignment.Center), FFar = Format(StringAlignment.Far);
    public static void TextAt(Graphics g, string text, Font f, Color color, RectangleF rect, StringAlignment align = StringAlignment.Near)
    {
        using var brush = new SolidBrush(color);
        g.DrawString(text, f, brush, rect, align switch
        {
            StringAlignment.Center => FCenter,
            StringAlignment.Far => FFar,
            _ => FNear
        });
    }
    public static Label Label(string text, float size = 10, bool bold = false, Color? color = null) =>
        new()
        {
            Text = text,
            UseMnemonic = false,
            Font = Font(size, bold),
            ForeColor = color ?? Text,
            AutoSize = true,
            BackColor = Color.Transparent
        };

    private const int DwmUseDark = 20, DwmBorder = 34, DwmCaption = 35, DwmText = 36, DwmCorner = 33;
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    private static int Colorref(Color c) => c.R | (c.G << 8) | (c.B << 16);
    public static void Chrome(Form f, bool captionToo = true)
    {
        try
        {
            int on = 1, round = 2, cap = Colorref(Sidebar), border = Colorref(Line), text = Colorref(Text);
            DwmSetWindowAttribute(f.Handle, DwmUseDark, ref on, 4);
            if (captionToo)
            {
                DwmSetWindowAttribute(f.Handle, DwmCaption, ref cap, 4);
                DwmSetWindowAttribute(f.Handle, DwmText, ref text, 4);
            }
            DwmSetWindowAttribute(f.Handle, DwmBorder, ref border, 4);
            DwmSetWindowAttribute(f.Handle, DwmCorner, ref round, 4);
        }
        catch { }
    }
}

internal abstract class UiControl : Control
{
    protected UiControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Bg;
    }
    protected float K => DeviceDpi / 96f;
    protected float W => Width / K;
    protected float H => Height / K;
    protected PointF L(MouseEventArgs e) => new(e.X / K, e.Y / K);
    protected sealed override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.ScaleTransform(K, K);
        Draw(g);
    }
    protected abstract void Draw(Graphics g);
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Invalidate();
    }
}
