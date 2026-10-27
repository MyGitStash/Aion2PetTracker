using System.Buffers.Binary;

namespace Aion2PetTracker.Core.Pets;

public enum SoulCurrencyKind
{
    Cogni, Fera, Natura, Varian, Special, Rainbow
}
public sealed record SoulCurrencyBalance(SoulCurrencyKind Kind, long? Count);
public sealed record SoulCurrencyView(IReadOnlyList<SoulCurrencyBalance> Balances, bool Synchronized, string Status, bool Cached = false, long VerifiedMs = 0, string? Character = null, bool Uncertain = false)
{
    public static SoulCurrencyView Unknown => new(Enum.GetValues<SoulCurrencyKind>().Select(k => new SoulCurrencyBalance(k, null)).ToList(), false, "Waiting for crystal balances");
    public long? Count(SoulCurrencyKind kind) => Balances.FirstOrDefault(b => b.Kind == kind)?.Count;
}

/// <summary>Exact wallet segment located immediately after the validated pet lists, not a scan for plausible
/// numbers. Observed Global 2026-10-26: count=6, six distinct (id u8, balance u32). The subsequent genus
/// knowledge fields are separate. 1190 uses (id u8, gain u32) and a trailing 02 in verified gain messages.</summary>
public static class SoulCurrencyProtocol
{
    public const ushort Changed = 0x1190;
    public static SoulCurrencyKind? Kind(byte id) => id switch
    {
        1 => SoulCurrencyKind.Cogni,
        2 => SoulCurrencyKind.Fera,
        3 => SoulCurrencyKind.Natura,
        4 => SoulCurrencyKind.Varian,
        5 => SoulCurrencyKind.Special,
        6 => SoulCurrencyKind.Rainbow,
        _ => null
    };
    public static bool TrySnapshot(ReadOnlySpan<byte> body, out Dictionary<SoulCurrencyKind, long> balances)
    {
        balances = [];
        if (!PetSoulProtocol.TryCollection(body, out _, out int offset) || body.Length - offset < 31 || body[offset++] != 6)
            return false;
        for (int i = 0; i < 6; i++)
        {
            var kind = Kind(body[offset]);
            uint count = BinaryPrimitives.ReadUInt32LittleEndian(body[(offset + 1)..]);
            offset += 5;
            if (kind is null || count > 1_000_000_000 || !balances.TryAdd(kind.Value, count))
                return false;
        }
        return balances.Count == 6;
    }
    public static bool TryGains(ReadOnlySpan<byte> body, out Dictionary<SoulCurrencyKind, long> gains)
    {
        gains = [];
        if (body.Length < 7 || body[0] is < 1 or > 6 || body.Length != 2 + body[0] * 5 || body[^1] != 2)
            return false;
        for (int at = 1; at < body.Length - 1; at += 5)
        {
            var kind = Kind(body[at]);
            uint amount = BinaryPrimitives.ReadUInt32LittleEndian(body[(at + 1)..]);
            if (kind is null || amount > 1_000_000 || !gains.TryAdd(kind.Value, amount))
                return false;
        }
        return true;
    }
}

/// <summary>Confirmed login balances plus confirmed currency gains. Never infer a reward from a pet's level.
/// Unsupported change shapes or TCP gaps invalidate the wallet instead of producing invented balances.</summary>
public sealed class SoulCurrencyTracker
{
    private Dictionary<SoulCurrencyKind, long> _balances = [];
    private long _last;
    private long _verifiedMs;
    private bool _exact, _uncertain;
    public CurrencySavedState Export() => new(new(_balances), _verifiedMs, _uncertain);
    public void Restore(CurrencySavedState state)
    {
        ResetFlow();
        _balances = new(state.Balances);
        _verifiedMs = state.VerifiedMs;
        _uncertain = state.Uncertain;
    }
    public long Snapshots
    {
        get; private set;
    }
    public long Updates
    {
        get; private set;
    }
    public long Rejected
    {
        get; private set;
    }
    public void ResetFlow()
    {
        _balances.Clear();
        _last = 0;
        _exact = false;
        _uncertain = false;
        _verifiedMs = 0;
    }
    public void Gap()
    {
        _exact = false;
        _uncertain = true;
    }
    public void OnFrame(long time, ushort opcode, ReadOnlySpan<byte> body)
    {
        if (opcode is not (PetSoulProtocol.Collection or SoulCurrencyProtocol.Changed) || time < _last)
            return;
        _last = time;
        if (opcode == PetSoulProtocol.Collection)
        {
            if (SoulCurrencyProtocol.TrySnapshot(body, out var balances))
            {
                _balances = balances;
                Snapshots++;
                _exact = true;
                _uncertain = false;
                _verifiedMs = time;
            }
            else
            {
                Gap();
                Rejected++;
            }
            return;
        }
        if (!SoulCurrencyProtocol.TryGains(body, out var gains))
        {
            Gap();
            Rejected++;
            return;
        }
        Updates++;
        if (_balances.Count != 6)
            return;
        if (gains.Any(p => _balances[p.Key] + p.Value > 1_000_000_000))
        {
            Gap();
            Rejected++;
            return;
        }
        foreach (var gain in gains)
            _balances[gain.Key] += gain.Value;
        if (_exact)
            _verifiedMs = time;
    }
    public SoulCurrencyView Snapshot() => new(Enum.GetValues<SoulCurrencyKind>().Select(k => new SoulCurrencyBalance(k, _balances.TryGetValue(k, out long n) ? n : null)).ToList(),
        _balances.Count == 6 && _exact, _balances.Count != 6 ? "Waiting for crystal balances" : _uncertain ? "Capture gap or unsupported update · Synchronization needed" : _exact ? "Crystal balances synchronized" : "Saved balances restored", _balances.Count == 6 && !_exact, _verifiedMs, Uncertain: _uncertain);
}
