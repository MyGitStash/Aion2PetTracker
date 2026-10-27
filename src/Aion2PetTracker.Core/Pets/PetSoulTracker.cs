namespace Aion2PetTracker.Core.Pets;

public sealed record PetSoulRow(int PetId, string Name, string Genus, PetProgress? Progress, int RecentGain, long LastGainMs, int SessionGain = 0, bool Cached = false, bool Uncertain = false, string? Notification = null);
public sealed record PetSoulEvent(long TimeMs, int PetId, string Name, int Amount);
public sealed record PetSoulView(bool Synchronized, long Gains, long Collections, string Status, IReadOnlyList<PetSoulRow> Rows,
    IReadOnlyList<PetSoulEvent>? RecentEvents = null);

/// <summary>One gameplay flow only, called under the capture service's packet gate. A new connection clears its baseline.
/// Totals require a full collection and uninterrupted reassembly. Unknown IDs keep a numeric label and unknown total.</summary>
public sealed class PetSoulTracker(PetCatalog catalog)
{
    public const long VisibleMs = 60_000;
    private Dictionary<int, long> _totals = [];
    private readonly Dictionary<int, (int Amount, long Time)> _recent = [];
    private readonly Dictionary<int, int> _sessionGains = [];
    private readonly Queue<PetSoulEvent> _events = new();
    private bool _exact, _complete, _uncertain;
    private readonly HashSet<int> _confirmedStages = [];
    private readonly HashSet<int> _newNotifications = [];
    public bool Synchronized => _exact;
    private long _lastMessage, _gains, _collections, _verifiedMs;
    public PetSavedState Export() => new(new(_totals), _verifiedMs, _complete, _uncertain);
    public void Restore(PetSavedState state)
    {
        ResetFlow();
        _totals = new(state.Totals);
        _verifiedMs = state.VerifiedMs;
        _complete = state.Complete;
        _uncertain = state.Uncertain;
    }
    public PetOwnedState Owned(int id) => new(_totals.TryGetValue(id, out var total) ? PetProgress.From(total) : _complete && catalog.Find(id) is not null ? PetProgress.From(0) : null, !_exact && !_confirmedStages.Contains(id), _uncertain && !_confirmedStages.Contains(id));
    public void ResetFlow()
    {
        _totals.Clear();
        _recent.Clear();
        _sessionGains.Clear();
        _events.Clear();
        _confirmedStages.Clear();
        _newNotifications.Clear();
        _exact = false;
        _complete = false;
        _uncertain = false;
        _lastMessage = 0;
        _verifiedMs = 0;
    }
    public void Gap()
    {
        _exact = false;
        _uncertain = true;
        _confirmedStages.Clear();
    }
    public void OnFrame(long time, ushort opcode, ReadOnlySpan<byte> body)
    {
        if (opcode is not (PetSoulProtocol.Collection or PetSoulProtocol.Gained or PetSoulProtocol.Registered or PetSoulProtocol.LevelChanged))
            return;
        if (time < _lastMessage)
            return;
        _lastMessage = time;
        foreach (int expired in _recent.Where(p => time - p.Value.Time >= VisibleMs).Select(p => p.Key).ToArray())
        {
            _recent.Remove(expired);
            _newNotifications.Remove(expired);
        }
        if (opcode is PetSoulProtocol.Registered or PetSoulProtocol.LevelChanged)
        {
            if (!PetSoulProtocol.TryStage(body, out int id, out int stage))
            {
                Gap();
                return;
            }
            if (!_totals.ContainsKey(id) && _totals.Count >= 512)
            {
                Gap();
                return;
            }
            long floor = stage switch
            {
                1 => 5,
                2 => 30,
                _ => 105
            };
            long previous = _totals.GetValueOrDefault(id);
            // Registration/level metadata is an absolute stage, not an extra batch of souls.
            // Repeated metadata and the preceding gain must not add the unlock cost twice.
            _totals[id] = Math.Max(previous, floor);
            if (previous <= floor)
                _confirmedStages.Add(id);
            if (opcode == PetSoulProtocol.Registered && previous < 5 && !_recent.ContainsKey(id) && _recent.Count < 32)
            {
                _recent[id] = (0, time);
                _newNotifications.Add(id);
            }
            return;
        }
        if (opcode == PetSoulProtocol.Collection)
        {
            if (!PetSoulProtocol.TryCollection(body, out var totals))
            {
                Gap();
                return;
            }
            _totals = totals;
            _exact = true;
            _complete = true;
            _uncertain = false;
            _confirmedStages.Clear();
            _collections++;
            _verifiedMs = time;
            return;
        }
        if (!PetSoulProtocol.TryGains(body, out var gains))
        {
            Gap();
            return;
        }
        foreach (var gain in gains)
        {
            _newNotifications.Remove(gain.PetId);
            _gains += gain.Amount;
            if (_events.Count >= 32)
                _events.Dequeue();
            _events.Enqueue(new(time, gain.PetId, catalog.Find(gain.PetId)?.Name ?? $"Pet #{gain.PetId}", gain.Amount));
            if (_sessionGains.ContainsKey(gain.PetId) || _sessionGains.Count < 512)
                _sessionGains[gain.PetId] = Math.Min(1_000_000, _sessionGains.GetValueOrDefault(gain.PetId) + gain.Amount);
            // Only a known ID absent from a complete baseline can start from zero.
            if (_totals.ContainsKey(gain.PetId) || (_complete && catalog.Find(gain.PetId) is not null))
                _totals[gain.PetId] = Math.Min(1_000_000, _totals.GetValueOrDefault(gain.PetId) + gain.Amount);
            if (_exact)
                _verifiedMs = time;
            var previous = _recent.GetValueOrDefault(gain.PetId);
            int amount = time - previous.Time < VisibleMs ? Math.Min(1_000_000, previous.Amount + gain.Amount) : gain.Amount;
            _recent[gain.PetId] = (amount, time);
        }
        if (_recent.Count > 32)
            foreach (int id in _recent.OrderByDescending(p => p.Value.Time).Skip(32).Select(p => p.Key).ToArray())
                _recent.Remove(id);
    }
    public PetSoulView Snapshot(long now)
    {
        // Dictionary insertion order keeps each pet in its own place; updates never swap rows.
        var rows = _recent.Where(p => now >= p.Value.Time && now - p.Value.Time < VisibleMs)
            .Select(p => new PetSoulRow(p.Key, catalog.Find(p.Key)?.Name ?? $"Pet #{p.Key}", catalog.Find(p.Key)?.Genus ?? "Unknown",
                _totals.TryGetValue(p.Key, out var total) ? PetProgress.From(total) : null, p.Value.Amount, p.Value.Time, _sessionGains.GetValueOrDefault(p.Key), Owned(p.Key).Cached, Owned(p.Key).Uncertain, _newNotifications.Contains(p.Key) ? "NEW" : null)).ToList();
        return new(_exact, _gains, _collections, _exact ? "Pet collection synchronized" : "Waiting for pet soul data", rows, _events.ToArray());
    }
}
