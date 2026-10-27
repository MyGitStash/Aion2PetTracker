using System.Reflection;
using Aion2PetTracker.Core.Capture;
using Aion2PetTracker.Core.Diagnostics;
using Aion2PetTracker.Core.Pets;
using Aion2PetTracker.Core.Protocol;

namespace Aion2PetTracker.App;

internal sealed record MerchantDiagnostics(long Accepted, long Rejected, long LastReceivedMs, byte? LastTabCode, MerchantTab? Tab, bool LastMessageRejected, long IgnoredOtherFlow);
internal sealed record TrackerSnapshot(string Capture, string? Character, ushort? Server, bool Identified, PetSoulView Pets, SoulCurrencyView Wallet, long Packets, long Frames, long Gaps, string? SaveError, bool SavePending, MerchantView? Merchant, MerchantDiagnostics Shop);
internal sealed class PetTrackerService : IDisposable
{
    private readonly object _gate = new();
    private readonly LiveSniffer _sniffer = new(new());
    private readonly DumpSession _session = new(OpcodeBook.Empty, new DumpOptions(), null);
    private readonly SoulStateStore _store;
    private readonly SoulTrackingSession _souls;
    private readonly MerchantTracker _merchant;
    private int _flowId;
    private long _gaps;
    private long _merchantIgnoredFrames;
    private bool _disposed;
    public static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public static PetCatalog LoadCatalog()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("pets.json")!;
        // The catalogue is part of the EXE; it never depends on a loose JSON file.
        using var reader = new StreamReader(stream);
        return PetCatalog.Parse(reader.ReadToEnd());
    }
    public PetTrackerService()
    {
        _store = new(Path.Combine(TrackerPreferences.Folder, "soul-state.json"));
        var catalog = LoadCatalog();
        _souls = new(catalog, _store);
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("shop-items.json")!)
        using (var reader = new StreamReader(stream))
            _merchant = new(new(MerchantProtocol.LoadItems(reader.ReadToEnd())), catalog);
        _session.Tap = OnFrame;
        _sniffer.PacketArrived += (raw, device) => { lock (_gate) { if (!_disposed) _session.OnPacket(raw, device.Name, device.Description); } };
        _sniffer.Start();
    }
    private void OnFrame(GameFlow flow, in FrameMeta meta, ReadOnlySpan<byte> body)
    {
        if (flow.Id < _flowId)
        {
            if (meta.Opcode == MerchantProtocol.Opcode)
                _merchantIgnoredFrames++;
            return;
        }
        if (flow.Id > _flowId)
        {
            if (_flowId != 0)
                _merchant.Reset();
            _flowId = flow.Id;
            _souls.BeginFlow(flow.Key.ToString());
        }
        if (_session.Tracker.Gaps != _gaps)
        {
            _gaps = _session.Tracker.Gaps;
            _souls.Gap();
        }
        if (meta.Opcode == 0x3336 && PlayerMessages.TryParseIdentity(body, true, out var identity))
            _souls.Identify(identity);
        _souls.OnFrame(flow.TimeMs, meta.Opcode, body);
        _merchant.OnFrame(flow.TimeMs, meta.Opcode, body);
    }
    public TrackerSnapshot Snapshot()
    {
        lock (_gate)
        {
            if (_session.Tracker.Gaps != _gaps)
            {
                _gaps = _session.Tracker.Gaps;
                _souls.Gap();
            }
            return new(_sniffer.Status, _souls.Character, _souls.ServerId, _souls.Identified, _souls.Pets.Snapshot(Now), _souls.WalletView(), _session.Packets, _session.Census.Ops.Values.Sum(s => s.Count), _gaps, _store.Error, _store.Pending, _merchant.View(Now, _souls.PetInfo, _souls.DisplayCharacter, _souls.Identified),
                new(_merchant.Accepted, _merchant.Rejected, _merchant.LastReceivedMs, _merchant.LastTabCode, _merchant.CurrentPage?.Tab, _merchant.LastMessageRejected, _merchantIgnoredFrames));
        }
    }
    public void Dispose()
    {
        _sniffer.Stop();
        lock (_gate)
        {
            _disposed = true;
            _session.Dispose();
        }
        _sniffer.Dispose();
        _store.Dispose();
    }
}
