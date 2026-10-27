using System.Reflection;
using System.Globalization;
using Aion2PetTracker.Core.Pets;

namespace Aion2PetTracker.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        ApplicationConfiguration.Initialize();
        int preview = Array.IndexOf(args, "--render-preview"), status = Array.IndexOf(args, "--status-file");
        using var mutex = new Mutex(true, preview >= 0 ? "Local\\Aion2PetTrackerPreview" : "Local\\Aion2PetTracker", out bool first);
        if (!first)
        {
            MessageBox.Show("Pet Tracker is already running. You can find its icon in the system tray.", "Aion2 Pet Tracker");
            return;
        }
        try
        {
            if (preview >= 0)
            {
                RenderPreviews(args[preview + 1]);
                return;
            }
            if (!NpcapSetup.EnsureInstalled())
                return;
            using var service = new PetTrackerService();
            Application.Run(new TrackerWindow(service, TrackerPreferences.Load(), status >= 0 ? args[status + 1] : null));
        }
        catch (Exception e)
        {
            Directory.CreateDirectory(TrackerPreferences.Folder);
            File.WriteAllText(Path.Combine(TrackerPreferences.Folder, "last-error.txt"), e.ToString());
            MessageBox.Show("Pet Tracker could not start.\nSee Daten/last-error.txt for details.", "Aion2 Pet Tracker", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.ExitCode = 1;
        }
    }
    internal static Icon LoadIcon()
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("tracker.ico")!;
        using var icon = new Icon(s);
        return (Icon)icon.Clone();
    }
    private static void RenderPreviews(string folder)
    {
        Directory.CreateDirectory(folder);
        NpcapSetup.RenderPreview(Path.Combine(folder, "setup-preview.png"));
        var prefs = new TrackerPreferences();
        using var overlay = new PetSoulsOverlay(prefs, () => { });
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var wallet = new SoulCurrencyView([new(SoulCurrencyKind.Cogni, 155), new(SoulCurrencyKind.Fera, 209), new(SoulCurrencyKind.Natura, 13), new(SoulCurrencyKind.Varian, 0), new(SoulCurrencyKind.Special, 0), new(SoulCurrencyKind.Rainbow, 100000)], true, "Preview");
        var pets = new PetSoulView(true, 1, 1, "Preview", [new(1003, "Forest Specter", "Natura", PetProgress.From(51), 1, now)]);
        overlay.RenderPreview(Path.Combine(folder, "pet-tracker-preview.png"), pets, wallet);
        overlay.RenderPreview(Path.Combine(folder, "pet-tracker-150pct-preview.png"), pets, wallet, 1.5f);
        overlay.RenderPreview(Path.Combine(folder, "pet-tracker-200pct-preview.png"), pets, wallet, 2);
        overlay.RenderPreview(Path.Combine(folder, "pet-tracker-cached-preview.png"), pets with
        {
            Rows = pets.Rows.Select(r => r with { Cached = true }).ToList()
        }, wallet with
        {
            Synchronized = false,
            Cached = true,
            VerifiedMs = now - 60000
        });
        overlay.RenderPreview(Path.Combine(folder, "pet-tracker-uncertain-preview.png"), pets with
        {
            Rows = pets.Rows.Select(r => r with { Cached = true, Uncertain = true }).ToList()
        }, wallet with
        {
            Synchronized = false,
            Cached = true,
            Uncertain = true,
            VerifiedMs = now - 60000
        });
        overlay.RenderPreview(Path.Combine(folder, "pet-tracker-registration-preview.png"), pets with
        {
            Rows = [new(1007, "Potcrab", "Fera", PetProgress.From(5), 0, now, Notification: "NEW")]
        }, wallet);
        overlay.RenderPreview(Path.Combine(folder, "pet-tracker-multifarm-preview.png"), pets with
        {
            Rows = [pets.Rows[0], new(1084, "Kalgolem", "Varian", PetProgress.From(40), 2, now), new(1096, "Superior Fire Spirit", "Natura", PetProgress.From(63), 1, now)]
        }, wallet);
        overlay.RenderPreview(Path.Combine(folder, "pet-tracker-header-preview.png"), pets with
        {
            Rows = []
        }, wallet);
        using var merchant = new MerchantOverlay(prefs, () => { });
        var shop = new MerchantView(MerchantTab.Pets, now, now + 60000, "PREVIEW", false, [
            new(1007,"Potcrab","Fera",PetProgress.From(83),true,80000),new(1054,"Mumu Warrior","Cogni",PetProgress.From(36),true,90000),
            new(1055,"Grave Rat","Fera",PetProgress.From(105),true,90000),new(1060,"Starturtle","Natura",PetProgress.From(25),true,80000),
            new(1064,"Oculazen","Varian",PetProgress.From(60),true,70000),new(1161,"Faded Floater","Varian",PetProgress.From(12),true,70000),
            new(1153,"Drana Mutant Brute","Fera",PetProgress.From(36),true,80000),new(1164,"Lesser Earth Spirit","Natura",PetProgress.From(0),true,80000),
            new(1199,"Flame Barghest","Fera",null,true,50000),new(1180,"Faded Mutant","Fera",PetProgress.From(78),true,90000)]);
        merchant.RenderPreview(Path.Combine(folder, "merchant-pets-preview.png"), shop);
        merchant.RenderPreview(Path.Combine(folder, "merchant-pets-150pct-preview.png"), shop, 1.5f);
        merchant.RenderPreview(Path.Combine(folder, "merchant-special-preview.png"), shop with
        {
            Tab = MerchantTab.Special,
            Rows = shop.Rows.Take(4).ToList()
        });
    }
}
