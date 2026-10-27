using Aion2PetTracker.Core.Pets;
namespace Aion2PetTracker.Tests;

public class ShopHeaderSearchTests
{
    // Two different non-periodic shapes, with enough spacing to test subpixel
    // scaling without two template points collapsing onto the same pixel.
    private static HeaderSignature Signature()
    {
        var random = new Random(17);
        return new(Enumerable.Range(0, 2).Select(g => new HeaderGroup("Shape" + g,
            Enumerable.Range(0, 60).Select(i => new HeaderPoint(20 + g * 120 + i % 15 * 4,
                20 + i / 15 * 4, random.Next(20, 230))).ToList())).ToList());
    }
    private static byte[] Render(HeaderSignature signature, double scale, int ox, int oy, bool invert = false)
    {
        var pixels = new byte[700 * 180];
        foreach (var group in signature.Groups)
            foreach (var p in group.Points)
            {
                int x = (int)Math.Round(p.X * scale) + ox, y = (int)Math.Round(p.Y * scale) + oy;
                pixels[y * 700 + x] = (byte)(invert ? 255 - p.Value : p.Value);
            }
        return pixels;
    }
    [Theory]
    [InlineData(.85, 8, 4)]
    [InlineData(1.1, -4, 8)]
    [InlineData(1.3, 3, -3)]
    [InlineData(1.75, -7, 5)]
    [InlineData(2.35, 4, -4)]
    public void FindsUiScalesAndOffsetsMissingFromTheOldExactOriginCheck(double scale, int ox, int oy)
    {
        var signature = Signature();
        var pixels = Render(signature, scale, ox, oy);
        var result = ShopHeaderSearch.Find(signature, pixels, 700, 180, [1, 1.25, 1.5, 2]);
        Assert.True(result.Score >= .99, $"score={result.Score}; scale={result.Scale}; offset={result.OffsetX},{result.OffsetY}");
        Assert.Equal(scale, result.Scale, 2);
        Assert.Equal(ox, result.OffsetX);
        Assert.Equal(oy, result.OffsetY);
    }
    [Fact]
    public void DoesNotAcceptAnUnrelatedBlankOrInvertedHeader()
    {
        var signature = Signature();
        Assert.Equal(0, ShopHeaderSearch.Find(signature, new byte[700 * 180], 700, 180, [1]).Score);
        var random = new Random(44);
        var pixels = new byte[700 * 180];
        random.NextBytes(pixels);
        Assert.True(ShopHeaderSearch.Find(signature, pixels, 700, 180, [1]).Score < .78);
        Assert.True(ShopHeaderSearch.Find(signature, Render(signature, 1, 0, 0, true), 700, 180, [1]).Score < .78);
    }
    [Fact]
    public void BothShapesAreRequiredAndBuffersAreValidated()
    {
        var signature = Signature();
        var pixels = Render(signature with
        {
            Groups = [signature.Groups[0]]
        }, 1, 0, 0);
        Assert.True(ShopHeaderSearch.Find(signature, pixels, 700, 180, [1]).Score < .78);
        Assert.Equal(0, ShopHeaderSearch.Find(signature, pixels[..^1], 700, 180, [1]).Score);
        Assert.Equal(0, ShopHeaderSearch.Find(signature, [], -1, 180, [1]).Score);
    }
    [Theory]
    [InlineData(.85, 8, 4)]
    [InlineData(1.1, 3, 7)]
    [InlineData(1.5, 0, 0)]
    public void SearchesTheRealEmbeddedSignatureAtNonDefaultUiSizes(double scale, int ox, int oy)
    {
        var signature = System.Text.Json.JsonSerializer.Deserialize<HeaderSignature>(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "config", "shop-header-signature.json")))!;
        var match = ShopHeaderSearch.Find(signature, Render(signature, scale, ox, oy), 700, 180, [1, 1.25, 1.5, 2]);
        Assert.True(match.Score >= .78, $"real signature score={match.Score}");
    }
}
