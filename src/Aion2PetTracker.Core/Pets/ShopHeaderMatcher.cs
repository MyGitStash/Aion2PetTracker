namespace Aion2PetTracker.Core.Pets;

public sealed record HeaderPoint(int X, int Y, int Value);
public sealed record HeaderGroup(string Name, List<HeaderPoint> Points);
public sealed record HeaderSignature(List<HeaderGroup> Groups);
public static class ShopHeaderMatcher
{
    // Normalized luminance correlation: independent of brightness and no OCR/name guessing.
    // Both the language-independent Shop symbol and the Exit-button outline must match.
    public static double Score(HeaderSignature signature, Func<int, int, int> pixel)
    {
        if (signature.Groups.Count != 2)
            return 0;
        double result = 1;
        foreach (var group in signature.Groups)
        {
            int n = group.Points.Count;
            if (n < 30)
                return 0;
            double sx = 0, sy = 0, sxx = 0, syy = 0, sxy = 0;
            foreach (var p in group.Points)
            {
                double x = p.Value, y = pixel(p.X, p.Y);
                sx += x;
                sy += y;
                sxx += x * x;
                syy += y * y;
                sxy += x * y;
            }
            double variance = (n * sxx - sx * sx) * (n * syy - sy * sy);
            if (variance < 1)
                return 0;
            result = Math.Min(result, (n * sxy - sx * sy) / Math.Sqrt(variance));
        }
        return Math.Clamp(result, 0, 1);
    }
}
