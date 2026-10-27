using System.Buffers.Binary;
using Aion2PetTracker.Core.Pets;
using Aion2PetTracker.Core.Protocol;
namespace Aion2PetTracker.Tests;

public class RegistrationAndQualityTests
{
    static PetCatalog Catalog => new([new(1007, "Potcrab", "Fera"), new(1160, "Drana Contaminant", "Varian")]);
    static byte[] Stage(int id, int level)
    {
        byte[] b = new byte[10];
        b[0] = 1;
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(2), id);
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(6), level);
        return b;
    }
    static byte[] Gain(int id, int amount)
    {
        byte[] b = new byte[9];
        b[0] = 1;
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(1), id);
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(5), amount);
        return b;
    }
    [Fact]
    public void ObservedLevelChangeReadsPetAndStageBeforeItsTrailingFields()
    {
        // Passive 1490 body observed on 2026-10-26. No character/account fields.
        var body = Convert.FromHexString("000032040000020000000209000000D83A010000000000");
        Assert.True(PetSoulProtocol.TryStage(body, out int id, out int level));
        Assert.Equal(1074, id);
        Assert.Equal(2, level);
        var tracker = new PetSoulTracker(new PetCatalog([new(1074, "Kuru Worker", "Cogni")]));
        tracker.Restore(new(new()
        {
            [1074] = 29
        }, 100, true));
        tracker.OnFrame(200, PetSoulProtocol.LevelChanged, body);
        Assert.Equal(30, tracker.Owned(1074).Progress!.Total);
        Assert.Equal(0, tracker.Snapshot(200).Gains);
    }
    [Fact]
    public void IdleAndSavedDataDoNotBecomeUncertainButAreNotClaimedFresh()
    {
        var pets = new PetSoulTracker(Catalog);
        pets.Restore(new(new()
        {
            [1007] = 12
        }, 100, true));
        Assert.True(pets.Owned(1007).Cached);
        Assert.False(pets.Owned(1007).Uncertain);
        pets.Snapshot(99999999);
        Assert.False(pets.Owned(1007).Uncertain);
        Assert.False(pets.Synchronized);
        var wallet = new SoulCurrencyTracker();
        wallet.Restore(new(Enum.GetValues<SoulCurrencyKind>().ToDictionary(k => k, k => 100L), 100));
        Assert.True(wallet.Snapshot().Cached);
        Assert.False(wallet.Snapshot().Uncertain);
        Assert.False(wallet.Snapshot().Synchronized);
    }
    [Fact]
    public void CaptureGapPersistsAsUncertaintyAcrossRestart()
    {
        var pets = new PetSoulTracker(Catalog);
        pets.Restore(new(new()
        {
            [1007] = 12
        }, 100, true));
        pets.Gap();
        var restored = new PetSoulTracker(Catalog);
        restored.Restore(pets.Export());
        Assert.True(restored.Owned(1007).Uncertain);
        Assert.Equal(12, restored.Owned(1007).Progress!.Total);
        var wallet = new SoulCurrencyTracker();
        wallet.Restore(new(Enum.GetValues<SoulCurrencyKind>().ToDictionary(k => k, k => 100L), 100));
        wallet.Gap();
        var again = new SoulCurrencyTracker();
        again.Restore(wallet.Export());
        Assert.True(again.Snapshot().Uncertain);
        Assert.Equal(100, again.Snapshot().Count(SoulCurrencyKind.Rainbow));
    }
    [Fact]
    public void WholePetRegistrationUpdatesZeroAndShowsNewWithoutCountingFiveFarmDrops()
    {
        var pets = new PetSoulTracker(Catalog);
        pets.Restore(new(new()
        {
            [1007] = 0
        }, 100, true));
        pets.OnFrame(200, PetSoulProtocol.Registered, Stage(1007, 1));
        var row = pets.Snapshot(200).Rows.Single();
        Assert.Equal(5, row.Progress!.Total);
        Assert.Equal(1, row.Progress.Level);
        Assert.Equal(0, row.Progress.Current);
        Assert.Equal("NEW", row.Notification);
        Assert.Equal(0, pets.Snapshot(200).Gains);
        Assert.False(row.Uncertain);
        pets.OnFrame(200, PetSoulProtocol.Registered, Stage(1007, 1));
        Assert.Equal(5, pets.Owned(1007).Progress!.Total);
    }
    [Fact]
    public void LevelMetadataIsAbsoluteAndDoesNotDoubleCountPrecedingSoulGain()
    {
        var pets = new PetSoulTracker(Catalog);
        pets.Restore(new(new()
        {
            [1007] = 4
        }, 100, true));
        pets.OnFrame(200, PetSoulProtocol.Gained, Gain(1007, 1));
        pets.OnFrame(200, PetSoulProtocol.Registered, Stage(1007, 1));
        Assert.Equal(5, pets.Owned(1007).Progress!.Total);
        Assert.Equal(1, pets.Snapshot(200).Gains);
        pets.OnFrame(300, PetSoulProtocol.LevelChanged, Stage(1007, 2));
        Assert.Equal(30, pets.Owned(1007).Progress!.Total);
        pets.OnFrame(400, PetSoulProtocol.Gained, Gain(1007, 2));
        Assert.Equal(32, pets.Owned(1007).Progress!.Total);
        pets.OnFrame(500, PetSoulProtocol.LevelChanged, Stage(1007, 2));
        Assert.Equal(32, pets.Owned(1007).Progress!.Total);
    }
    [Fact]
    public void MalformedRegistrationDoesNotInventAnOwnedPet()
    {
        var pets = new PetSoulTracker(Catalog);
        pets.OnFrame(100, PetSoulProtocol.Registered, Stage(1007, 4));
        Assert.Null(pets.Owned(1007).Progress);
        Assert.False(PetSoulProtocol.TryStage(Stage(1007, 1)[..^1], out _, out _));
        Assert.False(PetSoulProtocol.TryStage(Stage(0, 1), out _, out _));
    }
    [Fact]
    public async Task RegistrationIsAssignedPersistedAndUpdatesMerchantIndependentlyOfToastTimeout()
    {
        string dir = Path.Combine(Path.GetTempPath(), "pet-registration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "state.json");
            using (var store = new SoulStateStore(path, false))
            {
                var s = new SoulTrackingSession(Catalog, store);
                s.BeginFlow("flow");
                s.OnFrame(100, PetSoulProtocol.Registered, Stage(1007, 1));
                s.Identify(new(2, "Alice", 0, PlayerFaction.Unknown, null, null, 1001));
                Assert.Equal(5, s.PetInfo(1007).Progress!.Total);
                Assert.Empty(s.Pets.Snapshot(100000).Rows);
                Assert.Equal(5, s.PetInfo(1007).Progress!.Total);
                await store.FlushAsync();
            }
            using var loaded = new SoulStateStore(path, false);
            var resumed = new SoulTrackingSession(Catalog, loaded);
            resumed.BeginFlow("flow");
            Assert.Equal(5, resumed.PetInfo(1007).Progress!.Total);
            Assert.False(resumed.PetInfo(1007).Uncertain);
        }
        finally { Directory.Delete(dir, true); }
    }
    [Fact]
    public void FreshFullCollectionClearsActualUncertainty()
    {
        var p = new PetSoulTracker(Catalog);
        p.Restore(new(new()
        {
            [1007] = 12
        }, 100, true));
        p.Gap();
        p.OnFrame(200, 0x0090, [0, 0, 0, 0, 0, 0]);
        Assert.False(p.Owned(1007).Uncertain);
        Assert.True(p.Synchronized);
    }
}
