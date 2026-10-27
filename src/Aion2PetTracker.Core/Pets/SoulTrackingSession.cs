using System.Text;
using Aion2PetTracker.Core.Protocol;

namespace Aion2PetTracker.Core.Pets;

/// <summary>Focus-independent session. Stable server/name identity excludes volatile entity IDs.
/// Before identity arrives, new data is kept in an unassigned flow scope rather than charged to the last character.</summary>
public sealed class SoulTrackingSession
{
    private readonly SoulStateStore _store;
    private readonly PetCatalog _catalog;
    private SoulCacheDocument _doc;
    private readonly Queue<(long Time, ushort Op, byte[] Body)> _pending = [];
    private string _flow = "";
    private string? _key;
    private SoulUnassigned? _resumable;
    public PetSoulTracker Pets
    {
        get;
    }
    public SoulCurrencyTracker Wallet { get; } = new();
    public string? Character
    {
        get; private set;
    }
    public ushort? ServerId
    {
        get; private set;
    }
    public bool Identified => _key is not null;
    public string? DisplayCharacter => Character ?? (Pets.Synchronized ? null : _doc.Profiles.FirstOrDefault(p => p.Key == _doc.LastKey)?.Name);
    public PetOwnedState PetInfo(int id)
    {
        var current = Pets.Owned(id);
        if (current.Progress is not null || Identified || Pets.Synchronized)
            return current;
        var saved = _doc.Profiles.FirstOrDefault(p => p.Key == _doc.LastKey)?.Pets;
        return saved is not null && saved.Totals.TryGetValue(id, out var total) ? new(PetProgress.From(total), true, saved.Uncertain) : saved?.Complete == true && _catalog.Find(id) is not null ? new(PetProgress.From(0), true, saved.Uncertain) : new(null, true);
    }
    public SoulTrackingSession(PetCatalog catalog, SoulStateStore store)
    {
        _catalog = catalog;
        Pets = new(catalog);
        _store = store;
        _doc = store.Snapshot();
    }
    public static string CharacterKey(ushort server, string name) => server + ":" + name.Normalize(NormalizationForm.FormC);
    public void BeginFlow(string flow)
    {
        _flow = flow;
        _key = null;
        Character = null;
        ServerId = null;
        _pending.Clear();
        Pets.ResetFlow();
        Wallet.ResetFlow();
        _resumable = _doc.Unassigned?.Flow == flow ? _doc.Unassigned : null;
        var previous = _doc.Profiles.FirstOrDefault(p => p.Key == _doc.LastKey && p.Flow == flow);
        if (previous is not null && _resumable is null)
        {
            _key = previous.Key;
            Character = previous.Name;
            ServerId = previous.ServerId;
            Pets.Restore(previous.Pets);
            Wallet.Restore(previous.Wallet);
        }
        // Unassigned data belongs only to this exact network connection, never to a guessed character.
        if (_resumable is not null)
        {
            Pets.Restore(_resumable.Pets);
            Wallet.Restore(_resumable.Wallet);
        }
    }
    public void Identify(PlayerIdentity identity)
    {
        if (identity.ServerId is not { } server || string.IsNullOrWhiteSpace(identity.Name))
            return;
        var key = CharacterKey(server, identity.Name);
        if (_key == key)
            return; // Riding, flight, map changes and entity-ID changes do not erase counts.
        bool first = _key is null;
        _key = key;
        Character = identity.Name;
        ServerId = server;
        var saved = _doc.Profiles.FirstOrDefault(p => p.Key == key);
        Pets.ResetFlow();
        Wallet.ResetFlow();
        if (saved is not null)
        {
            Pets.Restore(saved.Pets);
            Wallet.Restore(saved.Wallet);
        }
        if (first && _resumable is not null)
        {
            Pets.Restore(_resumable.Pets);
            Wallet.Restore(_resumable.Wallet);
        }
        if (first)
            foreach (var f in _pending)
            {
                Pets.OnFrame(f.Time, f.Op, f.Body);
                Wallet.OnFrame(f.Time, f.Op, f.Body);
            }
        _pending.Clear();
        _resumable = null;
        Save();
    }
    public void OnFrame(long time, ushort op, ReadOnlySpan<byte> body)
    {
        if (op is not (PetSoulProtocol.Collection or PetSoulProtocol.Gained or PetSoulProtocol.Registered or PetSoulProtocol.LevelChanged or SoulCurrencyProtocol.Changed))
            return;
        if (_key is null)
        {
            if (_pending.Count >= 512)
            {
                _pending.Clear();
                _resumable = new(_flow, Pets.Export(), Wallet.Export());
            }
            _pending.Enqueue((time, op, body.ToArray()));
        }
        Pets.OnFrame(time, op, body);
        Wallet.OnFrame(time, op, body);
        Save();
    }
    public void Gap()
    {
        Pets.Gap();
        Wallet.Gap();
        Save();
    }
    private void Save()
    {
        var pets = Pets.Export();
        var wallet = Wallet.Export();
        if (_key is { } key)
        {
            var profiles = _doc.Profiles.Where(p => p.Key != key).TakeLast(31).ToList();
            profiles.Add(new(key, Character!, ServerId!.Value, pets, wallet, _flow));
            _doc = new(1, key, profiles, null);
        }
        else if (pets.Totals.Count > 0 || wallet.Balances.Count > 0)
            _doc = _doc with
            {
                Unassigned = new(_flow, pets, wallet)
            };
        else
            return; // Empty traffic cannot overwrite the last known counts.
        _store.Queue(_doc);
    }
    public SoulCurrencyView WalletView()
    {
        var current = Wallet.Snapshot();
        if (current.Balances.Any(b => b.Count is not null))
            return current with
            {
                Character = Character ?? "Detecting character"
            };
        if (Identified)
            return current with
            {
                Character = Character
            };
        var last = _doc.Profiles.FirstOrDefault(p => p.Key == _doc.LastKey);
        var fallback = _doc.Unassigned?.Wallet.Balances.Count == 6 ? _doc.Unassigned.Wallet : last?.Wallet;
        if (fallback is null || fallback.Balances.Count != 6)
            return current;
        return new(Enum.GetValues<SoulCurrencyKind>().Select(k => new SoulCurrencyBalance(k, fallback.Balances[k])).ToList(), false,
            "Last saved balances · Character not yet identified", true, fallback.VerifiedMs, last?.Name ?? "Last saved balances", fallback.Uncertain);
    }
}
