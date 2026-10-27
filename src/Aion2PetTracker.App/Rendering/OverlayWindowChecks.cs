using System.Runtime.InteropServices;
using System.Text.Json;
using Aion2PetTracker.Core.Pets;

namespace Aion2PetTracker.App;

/// <summary>Opt-in native regression checks used by --render-preview. No capture or saved settings.</summary>
internal static class OverlayWindowChecks
{
    internal static void Run(string folder, TrackerPreferences prefs, PetSoulsOverlay souls,
        PetSoulView pets, SoulCurrencyView wallet, MerchantOverlay merchant, MerchantView offers)
    {
        prefs.OverlayOpacity = 0; // Exercise real windows without displaying demo data on the desktop.
        var scene = new MerchantScene(true, true, Screen.PrimaryScreen!.WorkingArea, 1);
        void SoulRefresh() => souls.RefreshState(pets, true, currencies: wallet);
        void MerchantRefresh() => merchant.RefreshState(offers, scene, true);
        CheckWindow(souls, SoulRefresh, souls.RestoreOverlay);
        CheckWindow(merchant, MerchantRefresh, merchant.Reopen);
        prefs.PetSoulsLocked = !prefs.PetSoulsLocked;
        SoulRefresh();
        MerchantRefresh();
        RequireTopmost(souls);
        RequireTopmost(merchant);
        Assert((GetWindowLongPtr(souls.Handle, -20).ToInt64() & 0x20) != 0, "Soul click-through was lost after handle recreation");
        Assert((GetWindowLongPtr(merchant.Handle, -20).ToInt64() & 0x20) != 0, "Merchant click-through was lost after handle recreation");
        merchant.RefreshState(offers, scene with { GameFocused = false }, true);
        Assert(merchant.Visible, "Pinned merchant must survive Alt-Tab with a last observed shop");
        merchant.RefreshState(offers, scene with { GameFocused = false, ShopVisible = false }, true);
        Assert(!merchant.Visible, "Merchant must not invent a shop scene");
        MerchantRefresh();
        prefs.AlwaysOnTop = false;
        SoulRefresh();
        MerchantRefresh();
        Assert(!IsTopmost(souls) && !IsTopmost(merchant), "Always-on-top opt-out did not remove native topmost style");
        merchant.RefreshState(offers, scene with { GameFocused = false }, true);
        Assert(!merchant.Visible, "Unpinned merchant must hide outside game focus");
        prefs.PetSouls = false;
        souls.RestoreOverlay();
        SoulRefresh();
        Assert(!souls.Visible, "Restoring must respect the overlay visibility preference");
        merchant.Reopen();
        merchant.RefreshState(offers, scene, false);
        Assert(!merchant.Visible, "Restoring must respect the merchant visibility preference");
        CheckMonitorPlacement(pets, wallet, offers);
        Assert(JsonSerializer.Deserialize<TrackerPreferences>("{}")!.AlwaysOnTop, "Existing settings must default to pinned overlays");
        Assert(!JsonSerializer.Deserialize<TrackerPreferences>(JsonSerializer.Serialize(new TrackerPreferences { AlwaysOnTop = false }))!.AlwaysOnTop,
            "Always-on-top opt-out must survive settings serialization");
        File.WriteAllText(Path.Combine(folder, "window-recovery.checks.json"), JsonSerializer.Serialize(new
        {
            nativeTopmostRecovery = true,
            minimizedWindowRecovery = true,
            displayChangeRecovery = true,
            noFocusActivation = true,
            clickThroughAfterHandleRecreation = true,
            pinnedMerchantSurvivesAltTab = true,
            topmostOptOut = true,
            visibilityPreferencesRespected = true,
            savedPositionsOnAllConnectedMonitors = true,
            settingsCompatibility = true,
            monitorCount = Screen.AllScreens.Length
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void CheckMonitorPlacement(PetSoulView pets, SoulCurrencyView wallet, MerchantView offers)
    {
        var gameScene = new MerchantScene(true, true, Screen.PrimaryScreen!.WorkingArea, 1);
        foreach (var monitor in Screen.AllScreens)
        {
            var area = monitor.WorkingArea;
            var prefs = new TrackerPreferences
            {
                OverlayOpacity = 0,
                PetSoulsX = area.Left + 20, PetSoulsY = area.Top + 20,
                MerchantX = area.Left + 20, MerchantY = area.Top + 100
            };
            using var souls = new PetSoulsOverlay(prefs, () => { });
            using var merchant = new MerchantOverlay(prefs, () => { });
            // A second refresh exercises the per-monitor DPI update after moving the handle.
            for (int i = 0; i < 2; i++)
            {
                souls.RefreshState(pets, true, currencies: wallet);
                merchant.RefreshState(offers, gameScene, true);
            }
            Assert(Screen.FromControl(souls).Bounds == monitor.Bounds && area.Contains(souls.Location), "Soul overlay lost its saved monitor");
            Assert(Screen.FromControl(merchant).Bounds == monitor.Bounds && area.Contains(merchant.Location), "Merchant overlay was pulled back to the game monitor");
        }
    }

    private static void CheckWindow(Form window, Action refresh, Action requestRestore)
    {
        var foreground = GetForegroundWindow();
        refresh();
        RequireTopmost(window);
        Assert(GetForegroundWindow() == foreground, "Showing an overlay stole focus");
        SetWindowPos(window.Handle, new(-2), 0, 0, 0, 0, 0x213);
        Assert(!IsTopmost(window), "Could not simulate topmost loss");
        requestRestore();
        refresh();
        RequireTopmost(window);
        Assert(GetForegroundWindow() == foreground, "Restoring an overlay stole focus");
        ShowWindow(window.Handle, 7); // SW_SHOWMINNOACTIVE
        Assert(IsIconic(window.Handle), "Could not simulate a minimized overlay");
        foreground = GetForegroundWindow();
        refresh();
        Assert(!IsIconic(window.Handle), "Overlay stayed minimized");
        Assert(GetForegroundWindow() == foreground, "Minimize recovery stole focus");
        SendMessage(window.Handle, 0x007E, IntPtr.Zero, IntPtr.Zero);
        refresh();
        RequireTopmost(window);
        Assert(GetForegroundWindow() == foreground, "Display-change recovery stole focus");
    }
    private static bool IsTopmost(Form window) => (GetWindowLongPtr(window.Handle, -20).ToInt64() & 0x8) != 0;
    private static void RequireTopmost(Form window) => Assert(window.Visible && IsTopmost(window), "Overlay was not visible and topmost");
    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr w, IntPtr l);
}
