using System.Text.Json;
using System.Diagnostics;
namespace Aion2PetTracker.App;

internal sealed class TrackerWindow : Form
{
    private readonly PetTrackerService _service;
    private readonly TrackerPreferences _prefs;
    private readonly PetSoulsOverlay _overlay;
    private readonly MerchantOverlay _merchant;
    private readonly MerchantSceneProbe _merchantScene = new();
    private MerchantScene _scene = new(false, false, Rectangle.Empty, 0);
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 250 };
    private readonly Label _character = Theme.Label("Detecting character automatically", 12, true);
    private readonly Label _state = Theme.Label("Waiting for game data", 9, color: Theme.Muted);
    private readonly Label _storage = Theme.Label("", 9, color: Theme.Muted);
    private readonly Label _capture = Theme.Label("", 9, color: Theme.Muted);
    private readonly string? _statusFile;
    private int _tick;
    private bool _exit;
    internal TrackerWindow(PetTrackerService service, TrackerPreferences prefs, string? statusFile)
    {
        _service = service;
        _prefs = prefs;
        _statusFile = statusFile;
        Text = "Aion2 Pet Tracker";
        Icon = Program.LoadIcon();
        ClientSize = new(500, 435);
        MinimumSize = new(516, 474);
        BackColor = Theme.Card;
        ForeColor = Theme.Text;
        StartPosition = FormStartPosition.CenterScreen;
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new(24), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        Controls.Add(panel);
        foreach (var label in new[] { _character, _state, _storage, _capture })
            label.MaximumSize = new(450, 0);
        panel.Controls.Add(Theme.Label("PET SOULS", 18, true));
        panel.Controls.Add(_character);
        panel.Controls.Add(_state);
        panel.Controls.Add(_capture);
        panel.Controls.Add(_storage);
        var visible = new CheckBox { Text = "Show pet overlay", Checked = prefs.PetSouls, AutoSize = true, Margin = new(0, 16, 0, 6), ForeColor = Theme.Text };
        visible.CheckedChanged += (_, _) => { prefs.PetSouls = visible.Checked; Save(); };
        panel.Controls.Add(visible);
        var locked = new CheckBox { Text = "Lock overlays · Click through to game", Checked = prefs.PetSoulsLocked, AutoSize = true, ForeColor = Theme.Text };
        locked.CheckedChanged += (_, _) => { prefs.PetSoulsLocked = locked.Checked; Save(); };
        panel.Controls.Add(locked);
        var merchant = new CheckBox { Text = "Show pet details at Cloud Merchant", Checked = prefs.MerchantInfo, AutoSize = true, ForeColor = Theme.Text };
        merchant.CheckedChanged += (_, _) => { prefs.MerchantInfo = merchant.Checked; Save(); };
        panel.Controls.Add(merchant);
        panel.Controls.Add(Theme.Label("Opacity", 9));
        var opacity = new TrackBar { Minimum = 30, Maximum = 100, Value = prefs.OverlayOpacity, Width = 420, TickStyle = TickStyle.None, Height = 30 };
        opacity.ValueChanged += (_, _) => { prefs.OverlayOpacity = opacity.Value; Save(); };
        panel.Controls.Add(opacity);
        panel.Controls.Add(Theme.Label("Saved balances are kept between sessions.\n≈ is shown only after capture gaps or unsupported updates.\nClosing settings keeps capture and autosave running in the system tray.", 9, color: Theme.Muted));
        _overlay = new(prefs, Save);
        _merchant = new(prefs, Save);
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show settings", null, (_, _) => { Show(); WindowState = FormWindowState.Normal; Activate(); });
        menu.Items.Add("Open data folder", null, (_, _) => Process.Start(new ProcessStartInfo(TrackerPreferences.Folder) { UseShellExecute = true }));
        menu.Items.Add("Exit", null, (_, _) => { _exit = true; Close(); });
        _tray = new()
        {
            Icon = Icon,
            Text = "Aion2 Pet Tracker",
            Visible = true,
            ContextMenuStrip = menu
        };
        _tray.DoubleClick += (_, _) => { Show(); WindowState = FormWindowState.Normal; Activate(); };
        _timer.Tick += (_, _) => UpdateState();
        _timer.Start();
        Shown += (_, _) => { Theme.Chrome(this); UpdateState(); };
    }
    private void Save()
    {
        try
        {
            _prefs.Save();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { _storage.Text = e is UnauthorizedAccessException ? "Could not save settings: access denied." : "Could not save settings: the file is unavailable."; }
    }
    private void UpdateState()
    {
        var s = _service.Snapshot();
        _overlay.RefreshState(s.Pets, true, false, s.Wallet);
        bool merchantEnabled = _prefs.PetSouls && _prefs.MerchantInfo;
        _scene = _merchantScene.Update(merchantEnabled && s.Merchant is not null, PetTrackerService.Now);
        _merchant.RefreshState(s.Merchant, _scene, merchantEnabled);
        if (_tick++ % 4 != 0)
            return;
        _character.Text = s.Character is { } name ? $"{name} · Server {s.Server}" : "Detecting character automatically";
        _state.Text = s.Identified ? s.Wallet.Status : s.Wallet.Cached ? "Saved data · Waiting for character information" : "The character is detected automatically from game data";
        _capture.Text = !Aion2PetTracker.Core.Capture.LiveSniffer.IsNpcapInstalled() ? "Npcap is missing · See README for setup" : s.Capture.Contains("not running") ? "Waiting for AION 2" : s.Packets > 0 ? "Receiving game data" : "Waiting for game data";
        _storage.Text = s.SaveError ?? (s.SavePending ? "Autosave pending" : "Autosave active · " + s.Packets.ToString("N0") + " packets received");
        if (_statusFile is not null)
            try
            {
                File.WriteAllText(_statusFile, JsonSerializer.Serialize(new
                {
                    release = "1",
                    at = PetTrackerService.Now,
                    Snapshot = s,
                    MerchantScene = _scene,
                    MerchantVisible = _merchant.Visible
                }, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (IOException) { }
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_exit && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnFormClosing(e);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _overlay.Dispose();
            _merchant.Dispose();
            _tray.Visible = false;
            _tray.ContextMenuStrip?.Dispose();
            _tray.Dispose();
        }
        base.Dispose(disposing);
    }
}
