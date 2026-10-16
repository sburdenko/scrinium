using System.Text.RegularExpressions;

namespace Scrinium.Core.Retrieval;

public static partial class NumberEvidence
{
    [GeneratedRegex(@"\d+(?:[., \u00A0\u202F\u2009]\d{3})*(?:[.,]\d+)?")]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"\p{L}[\p{L}\p{Mn}\p{Nd}_-]{2,}")]
    private static partial Regex WordPattern();

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

    public static HashSet<string> Words(string text) =>
        WordPattern().Matches(text)
            .Select(match => match.Value.ToLowerInvariant())
            .ToHashSet();

    /// <summary>
    /// The share of the claim's words (leaving out words the question already used) that appear in the sentence,
    /// so a sentence whose words the answer actually borrowed beats a title line that only echoes the question.
    /// </summary>
    public static double WordOverlap(string claim, string sentence, IReadOnlySet<string> alreadyAsked)
    {
        var claimWords = Words(claim);
        var wanted = new HashSet<string>(claimWords);
        wanted.RemoveWhere(w => MatchesAny(w, alreadyAsked));
        if (wanted.Count == 0)
            return 0;

        var present = Words(sentence);
        var hasQuestionWord = alreadyAsked.Count == 0
            || present.Any(w => w.Length >= 4 && MatchesAny(w, alreadyAsked));

        if (hasQuestionWord
            && sentence.AsSpan().TrimStart().StartsWith("|", StringComparison.Ordinal)
            && present.Count > 0
            && (double)present.Count(w => MatchesAny(w, claimWords)) / present.Count >= 0.75)
        {
            return 1;
        }

        var bridgesQuestionAndClaim = hasQuestionWord
            && present.Any(w => w.Length >= 5 && MatchesAny(w, alreadyAsked))
            && present.Any(w => w.Length >= 5 && MatchesAny(w, wanted));

        if (bridgesQuestionAndClaim)
            return 1;

        var contentWanted = wanted.Where(w => w.Length >= 5).ToList();
        if (contentWanted.Count >= 2 && contentWanted.All(w => MatchesAny(w, present)))
            return 1;

        if (!hasQuestionWord)
            return 0;

        return (double)wanted.Count(w => MatchesAny(w, present)) / wanted.Count;
    }

    private static bool MatchesAny(string word, IEnumerable<string> candidates) =>
        candidates.Any(c => SameWord(word, c));

    private static bool SameWord(string a, string b) =>
        a == b || (Math.Min(a.Length, b.Length) >= 5
            && Math.Abs(a.Length - b.Length) <= 2
            && (a.StartsWith(b, StringComparison.Ordinal) || b.StartsWith(a, StringComparison.Ordinal)));

    public static string StripUnmentionedNumbers(string text, IReadOnlySet<string> mentioned) =>
        NumberPattern().Replace(text, match =>
        {
            var digits = new string(match.Value.Where(char.IsDigit).ToArray());
            return digits.Length == 0 || mentioned.Contains(digits) ? match.Value : "";
        });
}
