using System.Text;
using System.Text.Json;

namespace Aion2PetTracker.Core.Storage;

/// <summary>Flush the new JSON before replacing the old file; retain one valid previous version.</summary>
public static class AtomicJsonFile
{
    public static void Write(string path, string json, Func<string, bool>? validatePrevious = null)
    {
        var target = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       4096, FileOptions.WriteThrough))
            {
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, leaveOpen: true))
                    writer.Write(json);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(target))
            {
                bool valid;
                try
                {
                    var oldText = File.ReadAllText(target);
                    using var old = JsonDocument.Parse(oldText);
                    valid = validatePrevious?.Invoke(oldText) ?? true;
                }
                catch (JsonException) { valid = false; }
                File.Replace(temporary, target, valid ? target + ".bak" : null, ignoreMetadataErrors: true);
            }
            else
                File.Move(temporary, target);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
