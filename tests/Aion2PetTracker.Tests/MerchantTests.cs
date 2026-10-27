using System.Buffers.Binary;
using Aion2PetTracker.Core.Pets;
namespace Aion2PetTracker.Tests;

public class MerchantTests
{
    static MerchantProtocol Protocol => new(new Dictionary<uint, int> { { 534760006, 1007 }, { 534760002, 1003 } });
    static PetCatalog Catalog => new([new(1007, "Potcrab", "Fera"), new(1003, "Forest Specter", "Natura")]);
    static byte[] Page(byte tab = 0xcc, bool pet = true)
    {
        var b = new byte[48];
        b[0] = tab;
        b[1] = 1;
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(2), 220006);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(6), pet ? 534760006u : 535710050u);
        BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(10), 1);
        b[18] = 1;
        b[19] = 0x5d;
        BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(20), 100000);
        BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(28), 80000);
        BinaryPrimitives.WriteInt64LittleEndian(b.AsSpan(36), 60000);
        return b;
    }
    [Theory]
    [InlineData(0xc9, MerchantTab.Special)]
    [InlineData(0xcc, MerchantTab.Pets)]
    [InlineData(0xcb, MerchantTab.Skins)]
    public void ReadsActualTabItemMappingAndDiscountedPrice(byte tab, MerchantTab expected)
    {
        Assert.True(Protocol.TryRead(100, Page(tab), out var page));
        Assert.Equal(expected, page!.Tab);
        Assert.Equal(1007, page.Offers.Single().PetId);
        Assert.Equal(80000, page.Offers.Single().Price);
        Assert.Equal(60000, page.ExpiresMs);
    }
    [Fact]
    public void SpecialWithNoPetItemsDoesNotCreateFakeRows()
    {
        var t = new MerchantTracker(Protocol, Catalog);
        t.OnFrame(100, 0x9156, Page(0xc9, false));
        Assert.Equal(1, t.Accepted);
        Assert.Null(t.View(200, _ => new(null, true), null, false));
    }
    [Fact]
    public void ShopOfferDoesNotMeanPetIsOwnedAndNeverChangesCounts()
    {
        var t = new MerchantTracker(Protocol, Catalog);
        t.OnFrame(100, 0x9156, Page());
        var row = t.View(200, _ => new(null, true), null, false)!.Rows.Single();
        Assert.Null(row.Progress);
        Assert.Equal("Potcrab", row.Name);
        Assert.Equal("Fera", row.Genus);
    }
    [Fact]
    public void ShowsCachedStageAndTotalSeparatelyAndRefreshesWithActualState()
    {
        var t = new MerchantTracker(Protocol, Catalog);
        t.OnFrame(100, 0x9156, Page());
        var before = t.View(200, _ => new(PetProgress.From(83), true), "Alice", false)!.Rows.Single();
        Assert.Equal(83, before.Progress!.Total);
        Assert.Equal(53, before.Progress.Current);
        Assert.True(before.Cached);
        var after = t.View(200, _ => new(PetProgress.From(84), false), "Alice", true)!.Rows.Single();
        Assert.Equal(84, after.Progress!.Total);
        Assert.False(after.Cached);
    }
    [Fact]
    public void SkinsNewFlowAndExpiryRemoveThePopupState()
    {
        var t = new MerchantTracker(Protocol, Catalog);
        t.OnFrame(100, 0x9156, Page());
        Assert.NotNull(t.View(59999, _ => new(null, true), null, false));
        Assert.Null(t.View(60000, _ => new(null, true), null, false));
        t.OnFrame(200, 0x9156, Page(0xcb, false));
        Assert.Null(t.View(201, _ => new(null, true), null, false));
        t.Reset();
        Assert.Null(t.CurrentPage);
    }
    [Fact]
    public void MalformedShopDataFailsClosedInsteadOfShowingPreviousOffers()
    {
        var t = new MerchantTracker(Protocol, Catalog);
        t.OnFrame(100, 0x9156, Page());
        var invalid = Page();
        invalid[1] = 2;
        t.OnFrame(200, 0x9156, invalid);
        Assert.Equal(1, t.Rejected);
        Assert.Null(t.View(201, _ => new(null, true), null, false));
        foreach (var b in new[] { Page()[..^1], Page(0xff) })
            Assert.False(Protocol.TryRead(100, b, out _));
        invalid = Page();
        invalid[^1] = 1;
        Assert.False(Protocol.TryRead(100, invalid, out _));
        invalid = Page();
        BinaryPrimitives.WriteUInt64LittleEndian(invalid.AsSpan(28), 100001);
        Assert.False(Protocol.TryRead(100, invalid, out _));
    }
    [Fact]
    public void LateOlderTabCannotReplaceNewerTab()
    {
        var t = new MerchantTracker(Protocol, Catalog);
        t.OnFrame(200, 0x9156, Page());
        t.OnFrame(100, 0x9156, Page(0xc9));
        Assert.Equal(MerchantTab.Pets, t.CurrentPage!.Tab);
    }
    [Fact]
    public void FullBaselineKnowsUnownedZeroAndKeepsItAsCachedAfterRestore()
    {
        var pets = new PetSoulTracker(Catalog);
        pets.OnFrame(100, 0x0090, [0, 0, 0, 0, 0, 0]);
        Assert.Equal(0, pets.Owned(1007).Progress!.Total);
        Assert.False(pets.Owned(1007).Cached);
        var saved = pets.Export();
        Assert.True(saved.Complete);
        pets.Restore(saved);
        Assert.Equal(0, pets.Owned(1007).Progress!.Total);
        Assert.True(pets.Owned(1007).Cached);
        pets.Restore(new([], 100));
        Assert.Null(pets.Owned(1007).Progress);
    }
    [Fact]
    public void HeaderMatcherNeedsTwoShapesAndRejectsBlankOrInvertedHeader()
    {
        var points = Enumerable.Range(0, 50).Select(i => new HeaderPoint(i, 0, i % 2 == 0 ? 20 : 160)).ToList();
        var signature = new HeaderSignature([new("Icon", points), new("Outline", points)]);
        Assert.True(ShopHeaderMatcher.Score(signature, (x, _) => x % 2 == 0 ? 40 : 180) > .99);
        Assert.Equal(0, ShopHeaderMatcher.Score(signature, (_, _) => 0));
        Assert.Equal(0, ShopHeaderMatcher.Score(signature, (x, _) => x % 2 == 0 ? 180 : 40));
        Assert.Equal(0, ShopHeaderMatcher.Score(new([new("OnlyOne", points)]), (x, _) => x % 2 == 0 ? 20 : 160));
    }
    [Fact]
    public async Task LegacyFullCollectionRetainsKnownZeroWithoutInventingCompletenessForPartialData()
    {
        string dir = Path.Combine(Path.GetTempPath(), "pet-merchant-upgrade-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "state.json");
        try
        {
            var profile = new SoulSavedProfile("1001:Alice", "Alice", 1001, new(new()
            {
                [1007] = 12
            }, 100), new([], 0));
            string legacy = System.Text.Json.JsonSerializer.Serialize(new SoulCacheDocument(1, profile.Key, [profile])).Replace(",\"Complete\":false", "");
            File.WriteAllText(path, legacy);
            using (var store = new SoulStateStore(path, false))
            {
                Assert.True(store.Snapshot().Profiles.Single().Pets.Complete);
                var s = new SoulTrackingSession(Catalog, store);
                s.BeginFlow("other");
                Assert.Equal(0, s.PetInfo(1003).Progress!.Total);
                Assert.True(s.PetInfo(1003).Cached);
                await store.FlushAsync();
            }
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new SoulCacheDocument(1, profile.Key, [profile])));
            using var partial = new SoulStateStore(path, false);
            Assert.False(partial.Snapshot().Profiles.Single().Pets.Complete);
        }
        finally { Directory.Delete(dir, true); }
    }
}
