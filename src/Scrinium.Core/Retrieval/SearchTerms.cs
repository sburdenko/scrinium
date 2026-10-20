using System.Text.RegularExpressions;

namespace Scrinium.Core.Retrieval;

/// <summary>
/// Picks the identifiers out of a question: codes, flags, paths, constants and quoted text, which embeddings match poorly.
/// Plain words are left to the meaning search.
/// </summary>
public static partial class SearchTerms
{
    private const int ShortestNumber = 3;

    private static readonly char[] LeadingPunctuation = ['(', '[', '{', '\'', '«', '„'];
    private static readonly char[] TrailingPunctuation = ['?', '!', ',', ';', ':', '.', ')', ']', '}', '\'', '»', '“'];

    [GeneratedRegex("`([^`]+)`|\"([^\"]+)\"|“([^”]+)”")]
    private static partial Regex Quoted();

    [GeneratedRegex(@"\S+")]
    private static partial Regex Token();

    public static IReadOnlyList<string> Identifiers(string question)
    {
        var quoted = Quoted().Matches(question)
            .Select(match => match.Groups.Values.Skip(1).First(group => group.Success).Value.Trim())
            .Where(text => text.Length > 0);
        var tokens = Token().Matches(Quoted().Replace(question, " "))
            .Select(match => match.Value.TrimStart(LeadingPunctuation).TrimEnd(TrailingPunctuation))
            .Where(IsIdentifier);

        return quoted.Concat(tokens).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsIdentifier(string word) =>
        word.Length >= 2
        && word.Any(char.IsLetterOrDigit)
        && ((word.Any(char.IsDigit) && word.Any(char.IsLetter))
            || (word.All(char.IsDigit) && word.Length >= ShortestNumber)
            || word.Count(char.IsUpper) >= 2
            || word.StartsWith('-')
            || word.Contains('_')
            || word.Contains('/')
            || word.Contains('.'));
}
