using System.Text.RegularExpressions;

namespace Scrinium.Core.Chat;

public static partial class CitationMarkers
{
    [GeneratedRegex(@"\[(\d+(?:\s*[,;]\s*\d+)*)\]")]
    private static partial Regex MarkerPattern();

    [GeneratedRegex(@"\s+([.,;:!?])")]
    private static partial Regex SpaceBeforePunctuation();

    public static IReadOnlyList<AnswerPart> Split(string text)
    {
        var parts = new List<AnswerPart>();
        var cursor = 0;

        foreach (Match match in MarkerPattern().Matches(text))
        {
            if (match.Index > cursor)
                parts.Add(new AnswerPart.Plain(text[cursor..match.Index]));

            var numbers = match.Groups[1].Value
                .Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .ToList();
            parts.Add(new AnswerPart.Marker(match.Value, numbers));
            cursor = match.Index + match.Length;
        }

        if (cursor < text.Length)
            parts.Add(new AnswerPart.Plain(text[cursor..]));

        return parts;
    }

    /// <summary>The text without its citation markers, with the spaces they leave behind tidied up.</summary>
    public static string Strip(string text)
    {
        var plain = string.Concat(Split(text).OfType<AnswerPart.Plain>().Select(p => p.Text));
        var spaced = string.Join(' ', plain.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return SpaceBeforePunctuation().Replace(spaced, "$1");
    }

    public static IReadOnlySet<int> CitedNumbers(string text) =>
        Split(text).OfType<AnswerPart.Marker>().SelectMany(m => m.Numbers).ToHashSet();
}
