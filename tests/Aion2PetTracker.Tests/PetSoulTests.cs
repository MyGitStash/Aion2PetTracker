using System.Buffers.Binary;
using Aion2PetTracker.Core.Pets;

namespace Aion2PetTracker.Tests;

public class PetSoulTests
{
    static PetCatalog Catalog => new([new(1002, "Krall Laborer", "Cogni"), new(1003, "Forest Specter", "Natura")]);
    static byte[] State(int id, int level, int points)
    {
        var b = new byte[26];
        b[4] = 1;
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(5), id);
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(9), id);
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(13), level);
        b[17] = 1;
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(18), id);
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(22), points);
        return b;
    }
    static byte[] Gain(int id, int amount = 1)
    {
        var b = new byte[9];
        b[0] = 1;
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(1), id);
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(5), amount);
        return b;
    }
    [Theory]
    [InlineData(0, 0, 0, 5)]
    [InlineData(4, 0, 4, 5)]
    [InlineData(5, 1, 0, 25)]
    [InlineData(29, 1, 24, 25)]
    [InlineData(30, 2, 0, 75)]
    [InlineData(104, 2, 74, 75)]
    [InlineData(105, 3, 0, 0)]
    public void ProgressUsesStageThresholds(int total, int level, int current, int goal)
    {
        var p = PetProgress.From(total);
        Assert.Equal(level, p.Level);
        Assert.Equal(current, p.Current);
        Assert.Equal(goal, p.Goal);
    }
    [Fact]
    public void DifferentPetsAndLevelUpAreTrackedIndependently()
    {
        var t = new PetSoulTracker(Catalog);
        t.OnFrame(100, PetSoulProtocol.Collection, State(1002, 1, 24));
        t.OnFrame(200, PetSoulProtocol.Gained, Gain(1002));
        t.OnFrame(300, PetSoulProtocol.Gained, Gain(1003, 2));
        var s = t.Snapshot(300);
        Assert.True(s.Synchronized);
        Assert.Equal(2, s.Rows.Count);
        Assert.Equal("Natura", s.Rows[1].Genus);
        Assert.Equal(2, s.Rows[1].Progress!.Current);
        Assert.Equal("Lvl 2 · 0 / 75", s.Rows[0].Progress!.Label);
    }
    [Fact]
    public void MidSessionMissingBaselineNeverInventsZeroOrTotal()
    {
        var t = new PetSoulTracker(Catalog);
        t.OnFrame(200, PetSoulProtocol.Gained, Gain(1002));
        Assert.Null(t.Snapshot(200).Rows.Single().Progress);
        Assert.Equal(1, t.Snapshot(200).Rows.Single().RecentGain);
        t.OnFrame(300, PetSoulProtocol.Collection, State(1002, 2, 20));
        Assert.Equal(50, t.Snapshot(300).Rows.Single().Progress!.Total);
    }
    [Fact]
    public void GainHasNoEffectOnOtherOpcodesOrAfterConnectionReset()
    {
        var t = new PetSoulTracker(Catalog);
        t.OnFrame(100, PetSoulProtocol.Collection, State(1002, 1, 5));
        t.OnFrame(200, 0x0538, Gain(1002));
        Assert.Empty(t.Snapshot(200).Rows);
        t.OnFrame(300, PetSoulProtocol.Gained, Gain(1002));
        t.ResetFlow();
        Assert.Empty(t.Snapshot(301).Rows);
        t.OnFrame(400, PetSoulProtocol.Gained, Gain(1002));
        Assert.Null(t.Snapshot(400).Rows.Single().Progress);
    }
    [Fact]
    public void GapOrMalformedMessageRequiresNewBaseline()
    {
        var t = new PetSoulTracker(Catalog);
        t.OnFrame(100, PetSoulProtocol.Collection, State(1002, 1, 5));
        t.Gap();
        t.OnFrame(200, PetSoulProtocol.Gained, Gain(1002));
        Assert.NotNull(t.Snapshot(200).Rows.Single().Progress);
        Assert.True(t.Snapshot(200).Rows.Single().Cached);
        t.OnFrame(300, PetSoulProtocol.Collection, State(1002, 1, 6));
        t.OnFrame(400, PetSoulProtocol.Gained, Gain(1002)[..^1]);
        Assert.NotNull(t.Snapshot(400).Rows.Single().Progress);
        Assert.True(t.Snapshot(400).Rows.Single().Cached);
        t.OnFrame(500, PetSoulProtocol.Collection, State(1002, 1, 6));
        Assert.Equal(11, t.Snapshot(500).Rows.Single().Progress!.Total);
    }
    [Fact]
    public void FullCollectionValidatesBoundsDuplicatesAndOpaqueTail()
    {
        var valid = State(1002, 1, 21);
        Assert.True(PetSoulProtocol.TryCollection([.. valid, 0xFF, 0xFF], out var totals));
        Assert.Equal(26, totals[1002]);
        Assert.False(PetSoulProtocol.TryCollection(valid[..^1], out _));
        var mismatch = valid.ToArray();
        mismatch[9] = 0;
        Assert.False(PetSoulProtocol.TryCollection(mismatch, out _));
        Assert.False(PetSoulProtocol.TryCollection(State(1002, 1, 25), out _));
        Assert.False(PetSoulProtocol.TryCollection(State(1002, 3, 1), out _));
        Assert.False(PetSoulProtocol.TryCollection([0, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0xFF, 0x1F, 0], out _));
    }
    [Fact]
    public void UnknownIdOnlyHasTotalIfExplicitInBaseline()
    {
        var t = new PetSoulTracker(Catalog);
        t.OnFrame(100, PetSoulProtocol.Collection, State(1002, 1, 5));
        t.OnFrame(200, PetSoulProtocol.Gained, Gain(5555));
        Assert.Equal("Pet #5555", t.Snapshot(200).Rows[0].Name);
        Assert.Null(t.Snapshot(200).Rows[0].Progress);
        t.OnFrame(300, PetSoulProtocol.Collection, State(5555, 1, 2));
        Assert.Equal(7, t.Snapshot(300).Rows[0].Progress!.Total);
    }
    [Fact]
    public void SamePetReusesRowAndToastExpiresWithoutNewPackets()
    {
        var t = new PetSoulTracker(Catalog);
        t.OnFrame(100, PetSoulProtocol.Collection, State(1002, 1, 5));
        t.OnFrame(200, PetSoulProtocol.Gained, Gain(1002));
        t.OnFrame(300, PetSoulProtocol.Gained, Gain(1002));
        Assert.Equal(2, t.Snapshot(300).Rows.Single().RecentGain);
        Assert.Equal(12, t.Snapshot(300).Rows.Single().Progress!.Total);
        Assert.Empty(t.Snapshot(60301).Rows);
    }
    [Fact]
    public void SessionGainSurvivesToastTimeoutButNeverPretendsToBeTotal()
    {
        var t = new PetSoulTracker(Catalog);
        t.OnFrame(100, PetSoulProtocol.Gained, Gain(1002, 2));
        t.OnFrame(61000, PetSoulProtocol.Gained, Gain(1002, 3));
        var row = t.Snapshot(61000).Rows.Single();
        Assert.Equal(5, row.SessionGain);
        Assert.Equal(3, row.RecentGain);
        Assert.Null(row.Progress);
        t.ResetFlow();
        t.OnFrame(62000, PetSoulProtocol.Gained, Gain(1002));
        Assert.Equal(1, t.Snapshot(62000).Rows.Single().SessionGain);
    }
    [Fact]
    public void ScreenshotCaseKeepsGraviKalgolemAndEarthSpiritSeparateEvenWithIdenticalTime()
    {
        var c = PetCatalog.Load(Path.Combine(AppContext.BaseDirectory, "config", "pets.json"));
        var t = new PetSoulTracker(c);
        byte[] batch = [3, .. Gain(1050, 8).Skip(1), .. Gain(1084, 1).Skip(1), .. Gain(1166, 1).Skip(1)];
        t.OnFrame(100, PetSoulProtocol.Gained, batch);
        var s = t.Snapshot(100);
        Assert.Equal(new[] { "Gravi", "Kalgolem", "Superior Earth Spirit" }, s.Rows.Select(r => r.Name));
        Assert.Equal(new[] { 8, 1, 1 }, s.Rows.Select(r => r.RecentGain));
        Assert.Equal(3, s.RecentEvents!.Count);
        t.OnFrame(30000, PetSoulProtocol.Gained, Gain(1084, 2));
        s = t.Snapshot(60099);
        Assert.Equal(new[] { 1050, 1084, 1166 }, s.Rows.Select(r => r.PetId));
        Assert.Equal(new[] { 8, 3, 1 }, s.Rows.Select(r => r.RecentGain));
        s = t.Snapshot(60100);
        Assert.Equal(1084, s.Rows.Single().PetId);
        Assert.Single(t.Snapshot(89999).Rows);
        Assert.Empty(t.Snapshot(90000).Rows);
    }
    [Fact]
    public void BatchIsAtomicAndOldTimeCannotDoubleCount()
    {
        var t = new PetSoulTracker(Catalog);
        t.OnFrame(100, PetSoulProtocol.Collection, State(1002, 1, 5));
        var batch = Gain(1002).Concat(Gain(1003, 0).Skip(1)).ToArray();
        batch[0] = 2;
        t.OnFrame(200, PetSoulProtocol.Gained, batch);
        Assert.Empty(t.Snapshot(200).Rows);
        t.OnFrame(300, PetSoulProtocol.Collection, State(1002, 1, 5));
        t.OnFrame(400, PetSoulProtocol.Gained, Gain(1002));
        t.OnFrame(350, PetSoulProtocol.Gained, Gain(1002));
        Assert.Equal(11, t.Snapshot(400).Rows[0].Progress!.Total);
    }
    [Fact]
    public void BundledCatalogueHasUniqueIdsAndPortraitMetadata()
    {
        var c = PetCatalog.Load(Path.Combine(AppContext.BaseDirectory, "config", "pets.json"));
        Assert.Equal(207, c.Count);
        Assert.Equal("Natura", c.Find(1003)!.Genus);
    }
}
