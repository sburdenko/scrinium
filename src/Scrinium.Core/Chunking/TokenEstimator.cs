namespace Scrinium.Core.Chunking;

public static class TokenEstimator
{
    private const double AsciiWeight = 0.25;
    private const double WideWeight = 0.5;

    public static double Weight(ReadOnlySpan<char> text)
    {
        var total = 0.0;
        foreach (var c in text)
            total += c < 128 ? AsciiWeight : WideWeight;
        return total;
    }

    public static int Estimate(ReadOnlySpan<char> text) => (int)Math.Ceiling(Weight(text));
}
