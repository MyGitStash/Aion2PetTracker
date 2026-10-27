using System.Text.Json;
using Aion2PetTracker.Core.Storage;
namespace Aion2PetTracker.App;

internal sealed class TrackerPreferences
{
    public bool PetSouls { get; set; } = true;
    public bool PetSoulsLocked
    {
        get; set;
    }
    public int? PetSoulsX
    {
        get; set;
    }
    public int? PetSoulsY
    {
        get; set;
    }
    public int OverlayOpacity { get; set; } = 90;
    public bool MerchantInfo { get; set; } = true;
    public bool MerchantManualDisplay
    {
        get; set;
    }
    public int? MerchantX
    {
        get; set;
    }
    public int? MerchantY
    {
        get; set;
    }
    public static string Folder => Path.Combine(AppContext.BaseDirectory, "Daten");
    public void Save() => AtomicJsonFile.Write(Path.Combine(Folder, "settings.json"), JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    public static TrackerPreferences Load()
    {
        foreach (var suffix in new[] { "", ".bak" })
            try
            {
                var p = Path.Combine(Folder, "settings.json" + suffix);
                if (!File.Exists(p))
                    continue;
                var value = JsonSerializer.Deserialize<TrackerPreferences>(File.ReadAllText(p));
                if (value is not null)
                {
                    value.OverlayOpacity = Math.Clamp(value.OverlayOpacity, 30, 100);
                    return value;
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { }
        return new();
    }
}
