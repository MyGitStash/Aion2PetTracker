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
    private readonly Label _shopState = Theme.Label("", 9, color: Theme.Muted);
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
        ShowInTaskbar = true;
        ClientSize = new(520, 650);
        MinimumSize = new(536, 500);
        BackColor = Theme.Card;
        ForeColor = Theme.Text;
        StartPosition = FormStartPosition.CenterScreen;
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new(24), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        Controls.Add(panel);
        foreach (var label in new[] { _character, _state, _storage, _capture, _shopState })
            label.MaximumSize = new(450, 0);
        panel.Controls.Add(Theme.Label("PET SOULS", 18, true));
        panel.Controls.Add(_character);
        panel.Controls.Add(_state);
        panel.Controls.Add(_capture);
        panel.Controls.Add(_storage);
        panel.Controls.Add(_shopState);
        var visible = new CheckBox { Text = "Show pet overlay", Checked = prefs.PetSouls, AutoSize = true, Margin = new(0, 16, 0, 6), ForeColor = Theme.Text };
        visible.CheckedChanged += (_, _) => { prefs.PetSouls = visible.Checked; Save(); };
        panel.Controls.Add(visible);
        var locked = new CheckBox { Text = "Lock overlays · Click through to game", Checked = prefs.PetSoulsLocked, AutoSize = true, ForeColor = Theme.Text };
        locked.CheckedChanged += (_, _) => { prefs.PetSoulsLocked = locked.Checked; Save(); };
        panel.Controls.Add(locked);
        var topmost = new CheckBox { Text = "Always on top · Keep overlays visible after Alt-Tab", Checked = prefs.AlwaysOnTop, AutoSize = true, ForeColor = Theme.Text };
        topmost.CheckedChanged += (_, _) => { prefs.AlwaysOnTop = topmost.Checked; RestoreOverlays(); Save(); };
        panel.Controls.Add(topmost);
        var restore = new Button { Text = "Restore overlays", AutoSize = true, Margin = new(0, 6, 0, 8) };
        restore.Click += (_, _) => RestoreOverlays();
        panel.Controls.Add(restore);
        var merchant = new CheckBox { Text = "Show pet details at Cloud Merchant", Checked = prefs.MerchantInfo, AutoSize = true, ForeColor = Theme.Text };
        merchant.CheckedChanged += (_, _) => { prefs.MerchantInfo = merchant.Checked; _merchant?.Reopen(); Save(); };
        panel.Controls.Add(merchant);
        var manual = new CheckBox { Text = "Show last merchant offers (manual mode)", Checked = prefs.MerchantManualDisplay, AutoSize = true, ForeColor = Theme.Text };
        manual.CheckedChanged += (_, _) => { prefs.MerchantManualDisplay = manual.Checked; _merchant?.Reopen(); Save(); };
        panel.Controls.Add(manual);
        panel.Controls.Add(Theme.Label("Fallback if the shop is not detected. Uses the last received offers.", 8, color: Theme.Muted));
        panel.Controls.Add(Theme.Label("Opacity", 9));
        var opacity = new TrackBar { Minimum = 30, Maximum = 100, Value = prefs.OverlayOpacity, Width = 420, TickStyle = TickStyle.None, Height = 30 };
        opacity.ValueChanged += (_, _) => { prefs.OverlayOpacity = opacity.Value; Save(); };
        panel.Controls.Add(opacity);
        panel.Controls.Add(Theme.Label("Saved balances are kept between sessions.\n≈ is shown only after capture gaps or unsupported updates.\nClosing settings minimizes to the taskbar. Use the tray menu to exit.", 9, color: Theme.Muted));
        _overlay = new(prefs, Save);
        _merchant = new(prefs, Save);
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show settings", null, (_, _) => ShowSettings());
        menu.Items.Add("Restore overlays", null, (_, _) => RestoreOverlays());
        menu.Items.Add("Open data folder", null, (_, _) => Process.Start(new ProcessStartInfo(TrackerPreferences.Folder) { UseShellExecute = true }));
        menu.Items.Add("Copy shop diagnostics", null, (_, _) => CopyShopDiagnostics());
        menu.Items.Add("Exit", null, (_, _) => { _exit = true; Close(); });
        _tray = new()
        {
            Icon = Icon,
            Text = "Aion2 Pet Tracker",
            Visible = true,
            ContextMenuStrip = menu
        };
        _tray.DoubleClick += (_, _) => ShowSettings();
        _timer.Tick += (_, _) => UpdateState();
        _timer.Start();
        Shown += (_, _) => { Theme.Chrome(this); UpdateState(); };
        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Normal)
                RestoreOverlays();
        };
    }
    private void ShowSettings()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        RestoreOverlays();
    }
    private void RestoreOverlays()
    {
        _overlay.RestoreOverlay();
        _merchant.Reopen();
        UpdateState();
    }
    private void Save()
    {
        try
        {
            _prefs.Save();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { _storage.Text = e is UnauthorizedAccessException ? "Could not save settings: access denied." : "Could not save settings: the file is unavailable."; }
    }
    private void CopyShopDiagnostics()
    {
        var s = _service.Snapshot();
        // Deliberately exclude character, server, balances, addresses and raw traffic.
        try
        {
            Clipboard.SetText(JsonSerializer.Serialize(new
            {
                AppVersion = "1.0.2",
                s.Shop,
                _scene.GameFocused,
                _scene.ShopVisible,
                ClientWidth = _scene.Bounds.Width,
                ClientHeight = _scene.Bounds.Height,
                _scene.Score,
                _scene.Scale,
                _scene.OffsetX,
                _scene.OffsetY,
                Dpi = DeviceDpi,
                _prefs.MerchantInfo,
                _prefs.MerchantManualDisplay,
                _prefs.AlwaysOnTop,
                HasPetOffers = s.Merchant is not null,
                PanelVisible = _merchant.Visible
            }, new JsonSerializerOptions { WriteIndented = true }));
            _shopState.Text = "Shop diagnostics copied · no character or account data included";
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            _shopState.Text = "Clipboard unavailable · try Copy shop diagnostics again";
        }
    }
    private void UpdateState()
    {
        var s = _service.Snapshot();
        _overlay.RefreshState(s.Pets, true, false, s.Wallet);
        bool merchantEnabled = _prefs.PetSouls && _prefs.MerchantInfo;
        _scene = _merchantScene.Update(merchantEnabled && s.Merchant is not null, PetTrackerService.Now, _prefs.MerchantManualDisplay);
        _merchant.RefreshState(s.Merchant, _scene, merchantEnabled);
        if (_tick++ % 4 != 0)
            return;
        _character.Text = s.Character is { } name ? $"{name} · Server {s.Server}" : "Detecting character automatically";
        _state.Text = s.Identified ? s.Wallet.Status : s.Wallet.Cached ? "Saved data · Waiting for character information" : "The character is detected automatically from game data";
        _capture.Text = !Aion2PetTracker.Core.Capture.LiveSniffer.IsNpcapInstalled() ? "Npcap is missing · See README for setup" : s.Capture.Contains("not running") ? "Waiting for AION 2" : s.Packets > 0 ? "Receiving game data" : "Waiting for game data";
        _storage.Text = s.SaveError ?? (s.SavePending ? "Autosave pending" : "Autosave active · " + s.Packets.ToString("N0") + " packets received");
        _shopState.Text = !merchantEnabled ? "Merchant panel disabled" : s.Merchant is null ?
            (s.Shop.LastMessageRejected ? "Shop data unavailable · reopen Pets or Special" : "Waiting for shop offers · open Pets or Special") :
            _merchant.Visible ? (!_scene.GameFocused ? "Merchant visible · last observed shop offers" :
            _prefs.MerchantManualDisplay ? "Merchant displayed · manual mode" : "Merchant displayed · automatic detection") :
            !_scene.GameFocused ? "Merchant ready · return to the game" : "Shop not detected · try Show last merchant offers (manual mode)";
        if (_statusFile is not null)
            try
            {
                File.WriteAllText(_statusFile, JsonSerializer.Serialize(new
                {
                    release = "1.0.2",
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
            WindowState = FormWindowState.Minimized;
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
