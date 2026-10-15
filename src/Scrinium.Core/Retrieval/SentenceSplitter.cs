namespace Scrinium.Core.Retrieval;

public static class SentenceSplitter
{
    private const int MinLength = 20;
    private const int MaxRunWithoutBreak = 160;

    public static IReadOnlyList<PassageSpan> Split(string text)
    {
        var raw = new List<PassageSpan>();
        var start = 0;

        for (var i = 0; i < text.Length; i++)
        {
            if (!EndsSentence(text, start, i))
                continue;

            AddTrimmed(raw, text, start, i + 1);
            start = i + 1;
        }

        AddTrimmed(raw, text, start, text.Length);
        return MergeShort(text, raw);
    }

    private static bool EndsSentence(string text, int start, int index)
    {
        var c = text[index];
        if (c == '\n')
            return LineBreakEndsPassage(text, start, index);

        var terminal = c is '.' or '!' or '?' or '…';
        return terminal && (index + 1 == text.Length || char.IsWhiteSpace(text[index + 1]));
    }

    private static bool IsTableSeparator(string text, int start, int end)
    {
        var line = text.AsSpan(start, end - start);
        return line[0] == '|' && line.Contains('-') && line.IndexOfAnyExcept("|-: ") < 0;
    }

    private static bool LineBreakEndsPassage(string text, int start, int index)
    {
        var lineStart = index == 0 ? 0 : text.LastIndexOf('\n', index - 1) + 1;
        var line = text.AsSpan(lineStart, index - lineStart).TrimStart();
        if (line.StartsWith("|") || IsHeading(line) || StartsBlankLine(text, index))
            return true;

        var last = index - 1;
        while (last >= start && char.IsWhiteSpace(text[last]))
            last--;

        if (last >= start && text[last] is '.' or '!' or '?' or '…' or ':')
            return true;

        var next = index + 1;
        while (next < text.Length && char.IsWhiteSpace(text[next]))
            next++;

        return StartsListItem(text, next) || (next < text.Length && text[next] == '|') || index - start >= MaxRunWithoutBreak;
    }

    private static bool IsHeading(ReadOnlySpan<char> line) => line.StartsWith("#");

    private static bool StartsBlankLine(string text, int newline)
    {
        var next = newline + 1;
        while (next < text.Length && text[next] is ' ' or '\t' or '\r')
            next++;

        return next < text.Length && text[next] == '\n';
    }

    private static bool StartsListItem(string text, int index)
    {
        if (index >= text.Length)
            return false;

        var c = text[index];
        if (c == '•')
            return true;

        var followedBySpace = index + 1 < text.Length && char.IsWhiteSpace(text[index + 1]);
        if (c is '-' or '–' or '—' or '*')
            return followedBySpace;

        return char.IsDigit(c) && index + 2 < text.Length && text[index + 1] is '.' or ')' && char.IsWhiteSpace(text[index + 2]);
    }

    private static void AddTrimmed(List<PassageSpan> spans, string text, int start, int end)
    {
        while (start < end && char.IsWhiteSpace(text[start]))
            start++;
        while (end > start && char.IsWhiteSpace(text[end - 1]))
            end--;

        if (end > start && !IsTableSeparator(text, start, end))
            spans.Add(new PassageSpan(start, end - start));
    }

    private static IReadOnlyList<PassageSpan> MergeShort(string text, List<PassageSpan> spans)
    {
        var merged = new List<PassageSpan>();
        var carry = (PassageSpan?)null;

        foreach (var span in spans)
        {
            if (text[span.Start] is '|' or '#')
            {
                if (carry is { } held)
                    merged.Add(held);

                carry = null;
                merged.Add(span);
                continue;
            }

            var current = carry is { } pending
                ? new PassageSpan(pending.Start, span.Start + span.Length - pending.Start)
                : span;

            if (current.Length < MinLength)
            {
                carry = current;
                continue;
            }

            merged.Add(current);
            carry = null;
        }

        if (carry is { } leftover)
        {
            if (merged.Count == 0)
                merged.Add(leftover);
            else
            {
                var last = merged[^1];
                merged[^1] = new PassageSpan(last.Start, leftover.Start + leftover.Length - last.Start);
            }
        }

        return merged;
    }
}
