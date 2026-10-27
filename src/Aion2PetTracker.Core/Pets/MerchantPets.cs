using System.Buffers.Binary;
using System.Text.Json;
namespace Aion2PetTracker.Core.Pets;

public sealed record PetOwnedState(PetProgress? Progress, bool Cached, bool Uncertain = false);
public enum MerchantTab
{
    Special, Pets, Skins
}
public sealed record MerchantOffer(uint OfferId, uint ItemId, int PetId, long Price);
public sealed record MerchantPage(MerchantTab Tab, long ReceivedMs, long ExpiresMs, IReadOnlyList<MerchantOffer> Offers);
public sealed record MerchantPetRow(int PetId, string Name, string Genus, PetProgress? Progress, bool Cached, long Price, bool Uncertain = false);
public sealed record MerchantView(MerchantTab Tab, long ReceivedMs, long ExpiresMs, string? Character, bool CharacterConfirmed, IReadOnlyList<MerchantPetRow> Rows);

/// <summary>Black Cloud shop 9156. Field shape/item mapping corroborated against pinned MIT reference and
/// live Global 2026-10-26. Current tab IDs differ from the reference: c9 Special, cc Pets, cb Skins.
/// Do not use Owned labels, truncated text, kills, or price previews to infer the player's soul count.</summary>
public sealed class MerchantProtocol(IReadOnlyDictionary<uint, int> items)
{
    public const ushort Opcode = 0x9156;
    public static Dictionary<uint, int> LoadItems(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("Items").EnumerateObject().ToDictionary(p => uint.Parse(p.Name), p => p.Value.GetInt32());
    }
    public bool TryRead(long receivedMs, ReadOnlySpan<byte> body, out MerchantPage? page)
    {
        page = null;
        if (body.Length is < 14 or > 16384)
            return false;
        MerchantTab? tab = body[0] switch
        {
            0xC9 => MerchantTab.Special,
            0xCC => MerchantTab.Pets,
            0xCB => MerchantTab.Skins,
            _ => null
        };
        int expected = body[1];
        if (tab is null || expected > 64)
            return false;
        long expires = BinaryPrimitives.ReadInt64LittleEndian(body[^12..]);
        if (expires < receivedMs || expires - receivedMs > 86_400_000 || BinaryPrimitives.ReadUInt32LittleEndian(body[^4..]) != 0)
            return false;
        var offers = new List<MerchantOffer>();
        var ids = new HashSet<uint>();
        int found = 0;
        for (int i = 6; i + 30 <= body.Length - 12; i++)
        {
            uint item = BinaryPrimitives.ReadUInt32LittleEndian(body[i..]);
            if (item is < 100_000_000 or > 999_999_999 || body[i + 13] != 0x5d)
                continue;
            ulong quantity = BinaryPrimitives.ReadUInt64LittleEndian(body[(i + 4)..]);
            ulong before = BinaryPrimitives.ReadUInt64LittleEndian(body[(i + 14)..]), after = BinaryPrimitives.ReadUInt64LittleEndian(body[(i + 22)..]);
            uint offer = BinaryPrimitives.ReadUInt32LittleEndian(body[(i - 4)..]);
            if (quantity is 0 or > 1_000_000 || before is 0 or > 1_000_000_000 || after > before || body[i + 12] > 3 || offer == 0 || !ids.Add(offer))
                return false;
            found++;
            if (items.TryGetValue(item, out int pet) && !offers.Any(p => p.PetId == pet))
                offers.Add(new(offer, item, pet, (long)after));
            i += 29;
        }
        if (found != expected)
            return false;
        page = new(tab.Value, receivedMs, expires, offers);
        return true;
    }
}
public sealed class MerchantTracker(MerchantProtocol protocol, PetCatalog catalog)
{
    private MerchantPage? _page;
    public long Accepted
    {
        get; private set;
    }
    public long Rejected
    {
        get; private set;
    }
    public MerchantPage? CurrentPage => _page;
    public void Reset() => _page = null;
    public void OnFrame(long time, ushort opcode, ReadOnlySpan<byte> body)
    {
        if (opcode != MerchantProtocol.Opcode)
            return;
        if (!protocol.TryRead(time, body, out var page))
        {
            Rejected++;
            _page = null;
            return;
        }
        if (_page is not null && time < _page.ReceivedMs)
            return;
        _page = page;
        Accepted++;
    }
    public MerchantView? View(long now, Func<int, PetOwnedState> owned, string? character, bool confirmed)
    {
        if (_page is not { } page || now < page.ReceivedMs || now >= page.ExpiresMs || page.Tab == MerchantTab.Skins || page.Offers.Count == 0)
            return null;
        return new(page.Tab, page.ReceivedMs, page.ExpiresMs, character, confirmed, page.Offers.Select(o =>
        {
            var pet = catalog.Find(o.PetId);
            var state = owned(o.PetId);
            return new MerchantPetRow(o.PetId, pet?.Name ?? $"Pet #{o.PetId}", pet?.Genus ?? "Unknown", state.Progress, state.Cached, o.Price, state.Uncertain);
        }).ToList());
    }
}
