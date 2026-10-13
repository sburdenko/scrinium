using System.Text.RegularExpressions;

namespace Scrinium.Core.Chat;

public abstract record AnswerPart
{
    private AnswerPart() { }

    public sealed record Plain(string Text) : AnswerPart;

    public sealed record Marker(string Raw, IReadOnlyList<int> Numbers) : AnswerPart;
}

public static partial class CitationMarkers
{
    [GeneratedRegex(@"\[(\d+(?:\s*[,;]\s*\d+)*)\]")]
    private static partial Regex MarkerPattern();

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

    public static IReadOnlySet<int> CitedNumbers(string text) =>
        Split(text).OfType<AnswerPart.Marker>().SelectMany(m => m.Numbers).ToHashSet();
}
