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
        return MergeShort(raw);
    }

    private static bool EndsSentence(string text, int start, int index)
    {
        var c = text[index];
        if (c == '\n')
            return LineBreakEndsPassage(text, start, index);

        var terminal = c is '.' or '!' or '?' or '…';
        return terminal && (index + 1 == text.Length || char.IsWhiteSpace(text[index + 1]));
    }

    private static bool LineBreakEndsPassage(string text, int start, int index)
    {
        var last = index - 1;
        while (last >= start && char.IsWhiteSpace(text[last]))
            last--;

        if (last >= start && text[last] is '.' or '!' or '?' or '…' or ':')
            return true;

        var next = index + 1;
        while (next < text.Length && char.IsWhiteSpace(text[next]))
            next++;

        return StartsListItem(text, next) || index - start >= MaxRunWithoutBreak;
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

        if (end > start)
            spans.Add(new PassageSpan(start, end - start));
    }

    private static IReadOnlyList<PassageSpan> MergeShort(List<PassageSpan> spans)
    {
        var merged = new List<PassageSpan>();
        var carry = (PassageSpan?)null;

        foreach (var span in spans)
        {
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
