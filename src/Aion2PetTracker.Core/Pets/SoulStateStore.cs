using System.Text.Json;
using Aion2PetTracker.Core.Storage;

namespace Aion2PetTracker.Core.Pets;

public sealed record PetSavedState(Dictionary<int, long> Totals, long VerifiedMs, bool Complete = false, bool Uncertain = false);
public sealed record CurrencySavedState(Dictionary<SoulCurrencyKind, long> Balances, long VerifiedMs, bool Uncertain = false);
public sealed record SoulSavedProfile(string Key, string Name, ushort ServerId, PetSavedState Pets, CurrencySavedState Wallet, string? Flow = null);
public sealed record SoulUnassigned(string Flow, PetSavedState Pets, CurrencySavedState Wallet);
public sealed record SoulCacheDocument(int Schema, string? LastKey, List<SoulSavedProfile> Profiles, SoulUnassigned? Unassigned = null)
{
    public static SoulCacheDocument Empty => new(1, null, []);
}

/// <summary>Only semantic counts and character names. Coalesced background writes, atomic replace,
/// one valid backup, retry after a locked/full destination. No disk writes on the capture thread.</summary>
public sealed class SoulStateStore : IDisposable
{
    private readonly object _gate = new(), _writeGate = new();
    private readonly Timer? _timer;
    private SoulCacheDocument _document = SoulCacheDocument.Empty;
    private long _revision, _saved;
    private bool _disposed;
    public string FilePath
    {
        get;
    }
    public string? Error
    {
        get; private set;
    }
    public bool Pending
    {
        get
        {
            lock (_gate)
                return _saved < _revision;
        }
    }
    public SoulStateStore(string path, bool background = true)
    {
        FilePath = path;
        foreach (var candidate in new[] { path, path + ".bak" })
        {
            try
            {
                if (!File.Exists(candidate))
                    continue;
                var json = File.ReadAllText(candidate);
                var doc = ReadDocument(json, out bool upgraded);
                if (!Valid(doc))
                {
                    Error = "Invalid soul backup";
                    continue;
                }
                _document = doc!;
                Error = candidate == path ? null : "Saved soul data recovered from backup";
                if (upgraded)
                    _revision = 1;
                break;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
                Error = "Could not read saved soul data: " + FailureReason(e);
            }
        }
        if (background)
            _timer = new Timer(_ => TrySave(), null, 1000, 1000);
    }
    private static SoulCacheDocument Clone(SoulCacheDocument d) => JsonSerializer.Deserialize<SoulCacheDocument>(JsonSerializer.Serialize(d))!;
    private static SoulCacheDocument? ReadDocument(string json, out bool upgraded)
    {
        upgraded = false;
        var doc = JsonSerializer.Deserialize<SoulCacheDocument>(json);
        if (doc is null || doc.Profiles is null)
            return doc;
        using var raw = JsonDocument.Parse(json);
        var root = raw.RootElement;
        // 1.0.0 could populate Totals only from a successfully validated full collection.
        // Keep its verified absence-as-zero knowledge on upgrade. Explicit partial records stay partial.
        bool Legacy(JsonElement parent) => parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty("Pets", out var pets) && pets.ValueKind == JsonValueKind.Object && !pets.TryGetProperty("Complete", out _);
        var profiles = doc.Profiles.ToList();
        if (root.TryGetProperty("Profiles", out var rows) && rows.ValueKind == JsonValueKind.Array)
        {
            int i = 0;
            foreach (var row in rows.EnumerateArray())
            {
                if (i >= profiles.Count)
                    break;
                var p = profiles[i];
                if (p?.Pets?.Totals?.Count > 0 && Legacy(row))
                {
                    profiles[i] = p with
                    {
                        Pets = p.Pets with
                        {
                            Complete = true
                        }
                    };
                    upgraded = true;
                }
                i++;
            }
        }
        var unassigned = doc.Unassigned;
        if (unassigned?.Pets?.Totals?.Count > 0 && root.TryGetProperty("Unassigned", out var u) && Legacy(u))
        {
            unassigned = unassigned with
            {
                Pets = unassigned.Pets with
                {
                    Complete = true
                }
            };
            upgraded = true;
        }
        return doc with
        {
            Profiles = profiles,
            Unassigned = unassigned
        };
    }
    public SoulCacheDocument Snapshot()
    {
        lock (_gate)
            return Clone(_document);
    }
    public void Queue(SoulCacheDocument document)
    {
        if (!Valid(document))
            throw new ArgumentException("Invalid soul cache", nameof(document));
        var copy = Clone(document);
        lock (_gate)
        {
            if (_disposed)
                return;
            _document = copy;
            _revision++;
        }
    }
    public static bool Valid(SoulCacheDocument? doc)
    {
        static bool Pets(PetSavedState? s) => s is not null && s.Totals is not null && s.Totals.Count <= 512 && s.VerifiedMs is >= 0 and <= 253402300799999 && s.Totals.All(p => p.Key > 0 && p.Value is >= 0 and <= 1_000_000);
        static bool Wallet(CurrencySavedState? s) => s is not null && s.Balances is not null && s.Balances.Count is 0 or 6 && s.VerifiedMs is >= 0 and <= 253402300799999 && s.Balances.All(p => Enum.IsDefined(p.Key) && p.Value is >= 0 and <= 1_000_000_000);
        if (doc is null || doc.Schema != 1 || doc.Profiles is null || doc.Profiles.Count > 32 || doc.Profiles.Any(p => p is null || string.IsNullOrWhiteSpace(p.Key) || p.Key.Length > 160 || string.IsNullOrWhiteSpace(p.Name) || p.Name.Length > 80 || p.ServerId is < 1000 or > 2999 || !Pets(p.Pets) || !Wallet(p.Wallet)))
            return false;
        if (doc.Profiles.Select(p => p.Key).Distinct(StringComparer.Ordinal).Count() != doc.Profiles.Count || doc.LastKey is not null && !doc.Profiles.Any(p => p.Key == doc.LastKey))
            return false;
        return doc.Unassigned is null || doc.Unassigned.Flow is not null && doc.Unassigned.Flow.Length <= 300 && Pets(doc.Unassigned.Pets) && Wallet(doc.Unassigned.Wallet);
    }
    private void TrySave()
    {
        if (!Monitor.TryEnter(_writeGate))
            return;
        try
        {
            SaveLatest();
        }
        finally { Monitor.Exit(_writeGate); }
    }
    private void SaveLatest()
    {
        SoulCacheDocument doc;
        long revision;
        lock (_gate)
        {
            if (_saved == _revision)
                return;
            doc = _document;
            revision = _revision;
        }
        try
        {
            // A semantically invalid primary must never replace the good backup.
            AtomicJsonFile.Write(FilePath, JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true }),
                json => { try { return Valid(ReadDocument(json, out _)); } catch (JsonException) { return false; } });
            lock (_gate)
            {
                _saved = revision;
                Error = null;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Error = "Soul backup pending: " + FailureReason(e);
        }
    }
    private static string FailureReason(Exception e) => e switch
    {
        UnauthorizedAccessException => "Access denied.",
        JsonException => "The saved file is not valid JSON.",
        _ => "The file is unavailable. Check permissions or free space."
    };
    public Task FlushAsync() => Task.Run(() => { lock (_writeGate) SaveLatest(); });
    public void Dispose()
    {
        _timer?.Dispose();
        lock (_gate)
            _disposed = true;
        FlushAsync().GetAwaiter().GetResult();
    }
}
