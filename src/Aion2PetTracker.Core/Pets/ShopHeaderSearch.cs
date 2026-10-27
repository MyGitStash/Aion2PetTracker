namespace Aion2PetTracker.Core.Pets;

public sealed record HeaderMatch(double Score, double Scale, int OffsetX, int OffsetY);

/// <summary>Bounded coarse-to-fine search for the two existing header shapes.
/// Uses a small luminance buffer instead of a native GetPixel call per sample.</summary>
public static class ShopHeaderSearch
{
    private readonly record struct Candidate(double Score, double Scale, int X, int Y);

    public static HeaderMatch Find(HeaderSignature signature, ReadOnlySpan<byte> pixels,
        int width, int height, IReadOnlyList<double> preferredScales)
    {
        if (width <= 0 || height <= 0 || (long)width * height != pixels.Length ||
            signature.Groups.Count != 2 || signature.Groups.Any(g => g.Points.Count < 30))
            return new(0, 1, 0, 0);
        var scales = preferredScales.Where(s => double.IsFinite(s) && s >= .5 && s <= 3).ToList();
        foreach (double scale in scales.Distinct())
        {
            double exact = Score(signature, pixels, width, height, scale, 0, 0, int.MaxValue);
            if (exact >= .995)
                return new(exact, scale, 0, 0);
        }
        for (int i = 10; i <= 60; i++)
            scales.Add(i / 20d);
        Candidate[] best = new Candidate[6];
        foreach (double scale in scales.Distinct())
            for (int y = -12; y <= 12; y++)
                for (int x = -12; x <= 12; x++)
                {
                    double score = Score(signature, pixels, width, height, scale, x, y, 64);
                    if (score >= .995)
                    {
                        double complete = Score(signature, pixels, width, height, scale, x, y, int.MaxValue);
                        if (complete >= .995)
                            return new(complete, scale, x, y);
                    }
                    if (score <= best[^1].Score)
                        continue;
                    var candidate = new Candidate(score, scale, x, y);
                    int at = best.Length - 1;
                    while (at > 0 && score > best[at - 1].Score)
                    {
                        best[at] = best[at - 1];
                        at--;
                    }
                    best[at] = candidate;
                }
        var match = new HeaderMatch(0, 1, 0, 0);
        foreach (var c in best.Where(c => c.Score > .35))
        {
            double score = Score(signature, pixels, width, height, c.Scale, c.X, c.Y, int.MaxValue);
            if (score > match.Score)
                match = new(score, c.Scale, c.X, c.Y);
            if (score >= .995)
                return match;
        }
        // Keep refinement bounded even on unrelated screens. Both complete shapes
        // still have to pass the original .78 threshold in the caller.
        foreach (var c in best.Take(3).Where(c => c.Score > .35))
            for (int step = -4; step <= 4; step++)
                for (int y = c.Y - 3; y <= c.Y + 3; y++)
                    for (int x = c.X - 3; x <= c.X + 3; x++)
                    {
                        double scale = c.Scale + step * .01;
                        if (scale < .5 || scale > 3)
                            continue;
                        double coarse = Score(signature, pixels, width, height, scale, x, y, 64);
                        if (coarse < .55)
                            continue;
                        double score = Score(signature, pixels, width, height, scale, x, y, int.MaxValue);
                        if (score > match.Score)
                            match = new(score, scale, x, y);
                        if (score >= .995)
                            return match;
                    }
        return match;
    }

    private static double Score(HeaderSignature signature, ReadOnlySpan<byte> pixels,
        int width, int height, double scale, int ox, int oy, int maximum)
    {
        double result = 1;
        foreach (var group in signature.Groups)
        {
            int n = Math.Min(maximum, group.Points.Count);
            double sx = 0, sy = 0, sxx = 0, syy = 0, sxy = 0;
            for (int i = 0; i < n; i++)
            {
                var p = group.Points[(int)((long)i * group.Points.Count / n)];
                int px = (int)Math.Round(p.X * scale) + ox, py = (int)Math.Round(p.Y * scale) + oy;
                if ((uint)px >= (uint)width || (uint)py >= (uint)height)
                    return 0;
                double a = p.Value, b = pixels[py * width + px];
                sx += a;
                sy += b;
                sxx += a * a;
                syy += b * b;
                sxy += a * b;
            }
            double variance = (n * sxx - sx * sx) * (n * syy - sy * sy);
            if (variance < 1)
                return 0;
            result = Math.Min(result, (n * sxy - sx * sy) / Math.Sqrt(variance));
        }
        return Math.Clamp(result, 0, 1);
    }
}
