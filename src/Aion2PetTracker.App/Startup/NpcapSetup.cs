using System.Diagnostics;
using Aion2PetTracker.Core.Capture;

namespace Aion2PetTracker.App;

/// <summary>
/// First-run guidance for the separately licensed capture driver. Nothing is
/// downloaded, installed or elevated by this application. The user opens the
/// official site and accepts the vendor's installer and Windows prompts.
/// </summary>
internal sealed class NpcapSetup : Form
{
    private readonly Label _status = Theme.Label("", 10, true);
    private readonly Button _continue = new() { Text = "Check again", AutoSize = true };

    internal NpcapSetup(bool preview = false)
    {
        Text = "Aion2 Pet Tracker · Setup";
        Icon = Program.LoadIcon();
        ClientSize = new(540, 320);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Card;
        ForeColor = Theme.Text;

        var content = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new(24),
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };
        Controls.Add(content);
        content.Controls.Add(Theme.Label("ONE-TIME SETUP", 18, true));
        content.Controls.Add(Theme.Label(
            "Npcap lets the tracker receive game messages.\n" +
            "Install it once from the official website, then return here.\n\n" +
            "1. Open the Npcap website and download its installer.\n" +
            "2. Follow the installer and Windows permission prompts.\n" +
            "3. Click Check again. Then start the game.", 10));
        var download = new Button { Text = "Open official Npcap download", AutoSize = true };
        download.Click += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://npcap.com/#download") { UseShellExecute = true });
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                _status.Text = "Open https://npcap.com in your browser.";
            }
        };
        content.Controls.Add(download);
        content.Controls.Add(_status);
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        _continue.Click += (_, _) => CheckInstallation();
        buttons.Controls.Add(_continue);
        var exit = new Button { Text = "Exit", AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(exit);
        content.Controls.Add(buttons);
        CancelButton = exit;
        AcceptButton = _continue;
        Shown += (_, _) => { Theme.Chrome(this); if (!preview) CheckInstallation(); };
    }

    private void CheckInstallation()
    {
        if (LiveSniffer.IsNpcapInstalled())
        {
            DialogResult = DialogResult.OK;
            Close();
            return;
        }
        _status.Text = "Npcap is not installed yet.";
    }

    internal static bool EnsureInstalled()
    {
        if (LiveSniffer.IsNpcapInstalled())
            return true;
        using var setup = new NpcapSetup();
        return setup.ShowDialog() == DialogResult.OK;
    }

    internal static void RenderPreview(string path)
    {
        using var setup = new NpcapSetup(preview: true);
        setup._status.Text = "Npcap is not installed yet.";
        setup.Show();
        Application.DoEvents();
        using var bitmap = new Bitmap(setup.Width, setup.Height);
        setup.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        setup.Close();
    }
}
