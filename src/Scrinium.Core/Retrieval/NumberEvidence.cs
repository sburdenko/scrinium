using System.Text.RegularExpressions;

namespace Scrinium.Core.Retrieval;

public static partial class NumberEvidence
{
    [GeneratedRegex(@"\d+(?:[., \u00A0\u202F\u2009]\d{3})*(?:[.,]\d+)?")]
    private static partial Regex NumberPattern();

    /// <summary>The numbers in a text as digit strings, so "1,200" and "1200" or "99.95" and "99,95" match.</summary>
    public static HashSet<string> Numbers(string text) =>
        NumberPattern().Matches(text)
            .Select(match => new string(match.Value.Where(char.IsDigit).ToArray()))
            .Where(digits => digits.Length > 0)
            .ToHashSet();

    /// <summary>Whether the claim states at least one number that the question did not already name.</summary>
    public static bool HasNewNumbers(string claim, IReadOnlySet<string> alreadyAsked)
    {
        var wanted = Numbers(claim);
        wanted.ExceptWith(alreadyAsked);
        return wanted.Count > 0;
    }

    /// <summary>
    /// The share of the claim's numbers that the sentence repeats, leaving out numbers the question already named
    /// and weighting each by its digit count: "99.95" says more about which sentence holds the fact than the
    /// "1" of "1 March".
    /// </summary>
    public static double Overlap(string claim, string sentence, IReadOnlySet<string> alreadyAsked)
    {
        var wanted = Numbers(claim);
        wanted.ExceptWith(alreadyAsked);
        if (wanted.Count == 0)
            return 0;

        var present = Numbers(sentence);
        return (double)wanted.Where(present.Contains).Sum(n => n.Length) / wanted.Sum(n => n.Length);
    }

    public static string StripUnmentionedNumbers(string text, IReadOnlySet<string> mentioned) =>
        NumberPattern().Replace(text, match =>
        {
            var digits = new string(match.Value.Where(char.IsDigit).ToArray());
            return digits.Length == 0 || mentioned.Contains(digits) ? match.Value : "";
        });
}
