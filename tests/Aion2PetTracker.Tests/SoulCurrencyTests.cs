using System.Buffers.Binary;
using Aion2PetTracker.Core.Pets;

namespace Aion2PetTracker.Tests;

public class SoulCurrencyTests
{
    static byte[] Snapshot(params uint[] counts)
    {
        // Empty valid pet lists; declared wallet follows at exactly byte 6, then unrelated genus data.
        var b = new byte[39];
        b[6] = 6;
        for (int i = 0; i < 6; i++)
        {
            b[7 + i * 5] = (byte)(i + 1);
            BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(8 + i * 5), counts[i]);
        }
        b[^2] = 0;
        b[^1] = 5;
        return b;
    }
    static byte[] Gain(byte kind, uint amount)
    {
        var b = new byte[7];
        b[0] = 1;
        b[1] = kind;
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(2), amount);
        b[6] = 2;
        return b;
    }
    [Fact]
    public void SnapshotKeepsAllSixBalancesAndRealZeroDistinctFromUnknown()
    {
        var t = new SoulCurrencyTracker();
        Assert.Null(t.Snapshot().Count(SoulCurrencyKind.Fera));
        t.OnFrame(100, 0x0090, Snapshot(10, 20, 30, 0, 0, 2500));
        var s = t.Snapshot();
        Assert.True(s.Synchronized);
        Assert.Equal(20, s.Count(SoulCurrencyKind.Fera));
        Assert.Equal(0, s.Count(SoulCurrencyKind.Special));
        Assert.Equal(2500, s.Count(SoulCurrencyKind.Rainbow));
    }
    [Fact]
    public void ReceivesSeparateGenusAndRainbowGainsWithoutTreatingThemAsAbsoluteTotals()
    {
        var t = new SoulCurrencyTracker();
        t.OnFrame(100, 0x0090, Snapshot(10, 20, 30, 0, 0, 2500));
        t.OnFrame(200, 0x1190, Gain(3, 7));
        t.OnFrame(300, 0x1190, Gain(6, 25));
        Assert.Equal(37, t.Snapshot().Count(SoulCurrencyKind.Natura));
        Assert.Equal(2525, t.Snapshot().Count(SoulCurrencyKind.Rainbow));
        Assert.Equal(20, t.Snapshot().Count(SoulCurrencyKind.Fera));
    }
    [Fact]
    public void MissingInitialCollectionNeverInventsZeroOrATotalFromAGain()
    {
        var t = new SoulCurrencyTracker();
        t.OnFrame(100, 0x1190, Gain(6, 25));
        Assert.Null(t.Snapshot().Count(SoulCurrencyKind.Rainbow));
        Assert.False(t.Snapshot().Synchronized);
    }
    [Fact]
    public void RejectsTruncatedDuplicateAndUnknownWalletEntries()
    {
        var valid = Snapshot(10, 20, 30, 0, 0, 2500);
        Assert.False(SoulCurrencyProtocol.TrySnapshot(valid[..25], out _));
        valid[12] = 1;
        Assert.False(SoulCurrencyProtocol.TrySnapshot(valid, out _));
        valid = Snapshot(10, 20, 30, 0, 0, 2500);
        valid[7] = 9;
        Assert.False(SoulCurrencyProtocol.TrySnapshot(valid, out _));
        valid = Snapshot(10, 20, 30, 0, 0, 2500);
        valid[6] = 5;
        Assert.False(SoulCurrencyProtocol.TrySnapshot(valid, out _));
    }
    [Fact]
    public void UnsupportedChangeAndCaptureGapInvalidateInsteadOfAssumingOperation()
    {
        var t = new SoulCurrencyTracker();
        t.OnFrame(100, 0x0090, Snapshot(10, 20, 30, 0, 0, 2500));
        var b = Gain(6, 5);
        b[^1] = 1;
        t.OnFrame(200, 0x1190, b);
        Assert.False(t.Snapshot().Synchronized);
        t.OnFrame(300, 0x0090, Snapshot(10, 20, 30, 0, 0, 2500));
        t.Gap();
        Assert.False(t.Snapshot().Synchronized);
        t.OnFrame(400, 0x0090, Snapshot(10, 20, 30, 0, 0, 2500));
        t.ResetFlow();
        Assert.False(t.Snapshot().Synchronized);
    }
    [Fact]
    public void CrystalRewardDoesNotIncrementAnyPetSoulRow()
    {
        var pets = new PetSoulTracker(new PetCatalog([new(1050, "Gravi", "Natura")]));
        pets.OnFrame(100, 0x1190, Gain(3, 7));
        Assert.Empty(pets.Snapshot(100).Rows);
        Assert.Equal(0, pets.Snapshot(100).Gains);
    }
    [Fact]
    public void OldFramesCannotReapplyAGainAfterNewerSnapshot()
    {
        var t = new SoulCurrencyTracker();
        t.OnFrame(200, 0x0090, Snapshot(10, 20, 30, 0, 0, 2500));
        t.OnFrame(100, 0x1190, Gain(6, 25));
        Assert.Equal(2500, t.Snapshot().Count(SoulCurrencyKind.Rainbow));
    }
}
