using System.Text.RegularExpressions;

namespace Scrinium.Core.Retrieval;

/// <summary>
/// Plain word counting that the embedding scores cannot replace: which words an answer shares with a sentence.
/// Content words have at least four letters, so "the", "ist" or "и" do not count. Two words are the same when they
/// share their beginning up to the last two letters of the shorter one, so "plan" matches "plans" and "refunds"
/// matches "refunded".
/// </summary>
public static partial class WordEvidence
{
    private const int MinLength = 4;
    private const int EndingLength = 2;

    [GeneratedRegex(@"\p{L}[\p{L}\p{Mn}\p{Nd}_-]*")]
    private static partial Regex WordPattern();

    public static IReadOnlyList<string> ContentWords(string text) =>
        WordPattern().Matches(text)
            .Select(match => match.Value.ToLowerInvariant())
            .Where(word => word.Length >= MinLength)
            .Distinct()
            .ToList();

    /// <summary>The Dice coefficient of the two texts' content words: twice the shared words over all words.</summary>
    public static double Likeness(string first, string second)
    {
        var a = ContentWords(first);
        var b = ContentWords(second);
        if (a.Count + b.Count == 0)
            return 0;

        var shared = a.Count(word => b.Any(other => Same(word, other)));
        return 2.0 * shared / (a.Count + b.Count);
    }

    /// <summary>The share of the text's content words that also appear in the source.</summary>
    public static double Coverage(string text, string source)
    {
        var words = ContentWords(text);
        if (words.Count == 0)
            return 0;

        var available = ContentWords(source);
        return (double)words.Count(word => available.Any(other => Same(word, other))) / words.Count;
    }

    /// <summary>The content words of the text that the other text does not contain, joined by spaces.</summary>
    public static string Remaining(string text, string other)
    {
        var present = ContentWords(other);
        return string.Join(' ', ContentWords(text).Where(word => !present.Any(p => Same(word, p))));
    }

    private static bool Same(string a, string b)
    {
        if (a == b)
            return true;

        var shorter = Math.Min(a.Length, b.Length);
        var needed = Math.Max(MinLength, shorter - EndingLength);
        return CommonPrefix(a, b) >= needed;
    }

    private static int CommonPrefix(string a, string b)
    {
        var length = 0;
        while (length < a.Length && length < b.Length && a[length] == b[length])
            length++;
        return length;
    }
}
