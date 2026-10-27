using System.Buffers.Binary;
using System.Text.Json;
using Aion2PetTracker.Core.Pets;
using Aion2PetTracker.Core.Protocol;

namespace Aion2PetTracker.Tests;

public sealed class SoulPersistenceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "aion-pets-test-" + Guid.NewGuid().ToString("N"));
    private string PathName => Path.Combine(_directory, "souls.json");
    private static PetCatalog Catalog => new([new(1002, "Pet A", "Fera"), new(1003, "Pet B", "Natura")]);
    private static PlayerIdentity Identity(string name = "Alice", ushort server = 1001, uint entity = 99) => new(entity, name, 0, PlayerFaction.Unknown, null, null, server);
    private static byte[] Collection(uint petPoints = 7, uint rainbow = 2500)
    {
        byte[] b = new byte[57];
        b[4] = 1;
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(5), 1002);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(9), 1002);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(13), 1);
        b[17] = 1;
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(18), 1002);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(22), petPoints);
        b[26] = 6;
        for (int i = 0; i < 6; i++)
        {
            b[27 + i * 5] = (byte)(i + 1);
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(28 + i * 5), i == 5 ? rainbow : (uint)i);
        }
        return b;
    }
    private static byte[] PetGain(int id = 1002, uint amount = 1)
    {
        byte[] b = new byte[9];
        b[0] = 1;
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(1), id);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(5), amount);
        return b;
    }
    private static byte[] CurrencyGain()
    {
        byte[] b = [1, 6, 25, 0, 0, 0, 2];
        return b;
    }
    private static SoulTrackingSession Session(SoulStateStore store)
    {
        var s = new SoulTrackingSession(Catalog, store);
        s.BeginFlow("connection-one");
        return s;
    }
    [Fact]
    public async Task RestartLoadsCountsAsCachedAndContinuesKnownGains()
    {
        using (var store = new SoulStateStore(PathName, false))
        {
            var s = Session(store);
            s.Identify(Identity());
            s.OnFrame(100, 0x0090, Collection());
            s.OnFrame(200, 0x0D90, PetGain());
            s.OnFrame(200, 0x1190, CurrencyGain());
            await store.FlushAsync();
            Assert.False(store.Pending);
        }
        using var reloaded = new SoulStateStore(PathName, false);
        var next = Session(reloaded);
        Assert.Equal(2525, next.WalletView().Count(SoulCurrencyKind.Rainbow));
        Assert.True(next.WalletView().Cached);
        Assert.False(next.WalletView().Synchronized);
        Assert.True(next.Identified);
        Assert.Equal("Alice", next.Character);
        next.OnFrame(300, 0x0D90, PetGain());
        next.OnFrame(300, 0x1190, CurrencyGain());
        Assert.Equal(14, next.Pets.Snapshot(300).Rows.Single().Progress!.Total);
        Assert.True(next.Pets.Snapshot(300).Rows.Single().Cached);
        Assert.Equal(2550, next.WalletView().Count(SoulCurrencyKind.Rainbow));
    }
    [Fact]
    public void SameCharacterWithDifferentEntityIdDoesNotResetBaseline()
    {
        using var store = new SoulStateStore(PathName, false);
        var s = Session(store);
        s.Identify(Identity());
        s.OnFrame(100, 0x0090, Collection());
        s.Identify(Identity(entity: 12345));
        s.OnFrame(200, 0x0D90, PetGain());
        Assert.Equal(13, s.Pets.Snapshot(200).Rows.Single().Progress!.Total);
        Assert.True(s.Pets.Snapshot(200).Synchronized);
        Assert.True(s.WalletView().Synchronized);
    }
    [Fact]
    public async Task CharactersAndServersHaveIsolatedBalancesAndReturnAutomatically()
    {
        using var store = new SoulStateStore(PathName, false);
        var s = Session(store);
        s.Identify(Identity());
        s.OnFrame(100, 0x0090, Collection(rainbow: 100));
        s.Identify(Identity("Bob"));
        Assert.Null(s.WalletView().Count(SoulCurrencyKind.Rainbow));
        s.OnFrame(200, 0x0090, Collection(rainbow: 200));
        s.Identify(Identity("Alice", 1002));
        Assert.Null(s.WalletView().Count(SoulCurrencyKind.Rainbow));
        s.OnFrame(300, 0x0090, Collection(rainbow: 300));
        s.Identify(Identity());
        Assert.Equal(100, s.WalletView().Count(SoulCurrencyKind.Rainbow));
        Assert.True(s.WalletView().Cached);
        await store.FlushAsync();
        Assert.Equal(3, store.Snapshot().Profiles.Count);
    }
    [Fact]
    public void UnknownCharacterNeverChargesLastCharactersWallet()
    {
        using var store = new SoulStateStore(PathName, false);
        var s = Session(store);
        s.Identify(Identity());
        s.OnFrame(100, 0x0090, Collection());
        s.BeginFlow("other-connection");
        s.OnFrame(200, 0x1190, CurrencyGain());
        Assert.Equal(2500, s.WalletView().Count(SoulCurrencyKind.Rainbow));
        Assert.True(s.WalletView().Cached);
        s.Identify(Identity("Bob"));
        Assert.Null(s.WalletView().Count(SoulCurrencyKind.Rainbow));
        Assert.Equal(2500, store.Snapshot().Profiles.Single(p => p.Name == "Alice").Wallet.Balances[SoulCurrencyKind.Rainbow]);
    }
    [Fact]
    public void CollectionBeforeIdentityIsReplayedOnceToCorrectCharacter()
    {
        using var store = new SoulStateStore(PathName, false);
        var s = Session(store);
        s.OnFrame(100, 0x0090, Collection());
        s.OnFrame(200, 0x1190, CurrencyGain());
        s.OnFrame(200, 0x0D90, PetGain());
        s.Identify(Identity());
        Assert.Equal(2525, s.WalletView().Count(SoulCurrencyKind.Rainbow));
        Assert.Equal(13, s.Pets.Snapshot(200).Rows.Single().Progress!.Total);
    }
    [Fact]
    public void GapRetainsVisibleCountsButAuthoritativeSnapshotReplacesThem()
    {
        using var store = new SoulStateStore(PathName, false);
        var s = Session(store);
        s.Identify(Identity());
        s.OnFrame(100, 0x0090, Collection());
        s.Gap();
        Assert.Equal(2500, s.WalletView().Count(SoulCurrencyKind.Rainbow));
        Assert.True(s.WalletView().Cached);
        s.OnFrame(200, 0x0090, Collection(rainbow: 7));
        Assert.Equal(7, s.WalletView().Count(SoulCurrencyKind.Rainbow));
        Assert.False(s.WalletView().Cached);
        Assert.True(s.WalletView().Synchronized);
    }
    [Fact]
    public async Task MalformedPrimaryRecoversBackupAndDoesNotOverwriteGoodBackup()
    {
        using (var store = new SoulStateStore(PathName, false))
        {
            var s = Session(store);
            s.Identify(Identity());
            s.OnFrame(100, 0x0090, Collection());
            await store.FlushAsync();
            s.OnFrame(200, 0x1190, CurrencyGain());
            await store.FlushAsync();
        }
        var validBackup = File.ReadAllText(PathName + ".bak");
        File.WriteAllText(PathName, "{\"Schema\":99,\"Profiles\":[]}");
        using var recovery = new SoulStateStore(PathName, false);
        Assert.NotEmpty(recovery.Snapshot().Profiles);
        var restored = Session(recovery);
        restored.Identify(Identity());
        restored.OnFrame(300, 0x1190, CurrencyGain());
        await recovery.FlushAsync();
        Assert.Equal(validBackup, File.ReadAllText(PathName + ".bak"));
        Assert.True(SoulStateStore.Valid(JsonSerializer.Deserialize<SoulCacheDocument>(File.ReadAllText(PathName))));
    }
    [Fact]
    public async Task FailedWriteRemainsPendingAndRetriesAfterDestinationIsAvailable()
    {
        Directory.CreateDirectory(PathName);
        using var store = new SoulStateStore(PathName, false);
        var s = Session(store);
        s.Identify(Identity());
        s.OnFrame(100, 0x0090, Collection());
        await store.FlushAsync();
        Assert.True(store.Pending);
        Assert.NotNull(store.Error);
        Directory.Delete(PathName);
        await store.FlushAsync();
        Assert.False(store.Pending);
        Assert.Null(store.Error);
        Assert.True(File.Exists(PathName));
    }
    [Fact]
    public async Task ForegroundVisibilityHasNoEffectOnTrackerAndTimeoutOnlyRemovesCards()
    {
        using var store = new SoulStateStore(PathName, false);
        var s = Session(store);
        s.Identify(Identity());
        s.OnFrame(100, 0x0090, Collection());
        s.OnFrame(200, 0x0D90, PetGain());
        Assert.Empty(s.Pets.Snapshot(100000).Rows);
        Assert.Equal(2500, s.WalletView().Count(SoulCurrencyKind.Rainbow));
        s.OnFrame(100001, 0x0D90, PetGain());
        Assert.Equal(14, s.Pets.Snapshot(100001).Rows.Single().Progress!.Total);
        await store.FlushAsync();
        Assert.Single(store.Snapshot().Profiles);
    }
    [Fact]
    public void UnknownOrTruncatedIdentityCannotBindAnOldProfile()
    {
        using var store = new SoulStateStore(PathName, false);
        var s = Session(store);
        s.Identify(Identity() with
        {
            ServerId = null
        });
        Assert.False(s.Identified);
        Assert.False(PlayerMessages.TryParseIdentity([1, 0], true, out _));
    }
    [Fact]
    public async Task UnassignedSameFlowSurvivesRestartAndIsBoundWithoutDoubleCounting()
    {
        using (var store = new SoulStateStore(PathName, false))
        {
            var s = Session(store);
            s.OnFrame(100, 0x0090, Collection());
            await store.FlushAsync();
        }
        using var reloaded = new SoulStateStore(PathName, false);
        var next = Session(reloaded);
        Assert.False(next.Identified);
        Assert.True(next.WalletView().Cached);
        next.OnFrame(200, 0x1190, CurrencyGain());
        next.Identify(Identity());
        Assert.Equal(2525, next.WalletView().Count(SoulCurrencyKind.Rainbow));
        Assert.Single(reloaded.Snapshot().Profiles);
    }
    [Fact]
    public async Task UnassignedOtherFlowIsNeverAssignedToANewCharacter()
    {
        using (var store = new SoulStateStore(PathName, false))
        {
            var s = Session(store);
            s.OnFrame(100, 0x0090, Collection());
            await store.FlushAsync();
        }
        using var reloaded = new SoulStateStore(PathName, false);
        var next = new SoulTrackingSession(Catalog, reloaded);
        next.BeginFlow("a-new-connection");
        next.Identify(Identity("Bob"));
        Assert.Null(next.WalletView().Count(SoulCurrencyKind.Rainbow));
    }
    [Fact]
    public void NamesUseStableUnicodeKeysAndInvalidSavedDatesAreRejected()
    {
        Assert.Equal(SoulTrackingSession.CharacterKey(1001, "\u00e9"), SoulTrackingSession.CharacterKey(1001, "e\u0301"));
        var invalid = new SoulCacheDocument(1, null, [], new("", new([], long.MaxValue), new([], 0)));
        Assert.False(SoulStateStore.Valid(invalid));
    }
    [Fact]
    public async Task BackgroundWriterSavesWithoutAWindowOrAnExplicitFlush()
    {
        using var store = new SoulStateStore(PathName);
        var s = Session(store);
        s.Identify(Identity());
        s.OnFrame(100, 0x0090, Collection());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        while (store.Pending)
            await Task.Delay(50, timeout.Token);
        Assert.True(File.Exists(PathName));
        Assert.True(SoulStateStore.Valid(JsonSerializer.Deserialize<SoulCacheDocument>(File.ReadAllText(PathName))));
    }
    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, true);
    }
}
