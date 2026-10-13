using Scrinium.Core.Documents;

namespace Scrinium.Core.Chunking;

public sealed class TextChunker(ChunkingOptions options)
{
    private static readonly string[] Separators =
        ["\n# ", "\n## ", "\n### ", "\n\n", "\n", ". ", "? ", "! ", "; ", ", ", " "];

    public TextChunker() : this(new ChunkingOptions()) { }

    public IReadOnlyList<ChunkDraft> Chunk(IReadOnlyList<TextSegment> segments) =>
        segments.SelectMany(ChunkSegment).ToList();

    private IEnumerable<ChunkDraft> ChunkSegment(TextSegment segment)
    {
        var text = segment.Text;
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        var ranges = Pack(text, 0, text.Length, 0);
        var previousStart = -1;

        foreach (var (rawStart, rawEnd) in ranges)
        {
            var start = previousStart < 0 ? rawStart : OverlapStart(text, previousStart, rawStart);
            previousStart = rawStart;

            var (trimmedStart, trimmedEnd) = Trim(text, start, rawEnd);
            if (trimmedEnd <= trimmedStart)
                continue;

            var slice = text[trimmedStart..trimmedEnd];
            yield return new ChunkDraft(segment.Page, trimmedStart, slice, TokenEstimator.Estimate(slice));
        }
    }

    private List<(int Start, int End)> Pack(string text, int start, int end, int separatorIndex)
    {
        if (Weight(text, start, end) <= options.MaxTokens)
            return [(start, end)];

        if (separatorIndex >= Separators.Length)
            return HardSplit(start, end);

        var parts = Cut(text, start, end, Separators[separatorIndex]);
        if (parts.Count == 1)
            return Pack(text, start, end, separatorIndex + 1);

        var result = new List<(int Start, int End)>();
        var run = (Start: -1, End: -1, Weight: 0.0);

        void Flush()
        {
            if (run.Start >= 0)
                result.Add((run.Start, run.End));
            run = (-1, -1, 0.0);
        }

        foreach (var (partStart, partEnd) in parts)
        {
            var weight = Weight(text, partStart, partEnd);
            if (weight > options.MaxTokens)
            {
                Flush();
                result.AddRange(Pack(text, partStart, partEnd, separatorIndex + 1));
            }
            else if (run.Start >= 0 && run.Weight + weight <= options.MaxTokens)
            {
                run = (run.Start, partEnd, run.Weight + weight);
            }
            else
            {
                Flush();
                run = (partStart, partEnd, weight);
            }
        }

        Flush();
        return result;
    }

    private static List<(int Start, int End)> Cut(string text, int start, int end, string separator)
    {
        var cutAfterSeparator = !separator.StartsWith("\n#", StringComparison.Ordinal);
        var parts = new List<(int Start, int End)>();
        var cursor = start;

        while (cursor < end)
        {
            var index = text.IndexOf(separator, cursor, end - cursor, StringComparison.Ordinal);
            if (index < 0)
                break;

            var cut = cutAfterSeparator ? index + separator.Length : index + 1;
            if (cut >= end)
                break;

            parts.Add((cursor, cut));
            cursor = cut;
        }

        parts.Add((cursor, end));
        return parts;
    }

    private List<(int Start, int End)> HardSplit(int start, int end)
    {
        var step = Math.Max(1, options.MaxTokens * 2);
        var result = new List<(int Start, int End)>();
        for (var cursor = start; cursor < end; cursor += step)
            result.Add((cursor, Math.Min(end, cursor + step)));
        return result;
    }

    private int OverlapStart(string text, int previousStart, int nextStart)
    {
        if (options.OverlapTokens <= 0)
            return nextStart;

        var cursor = nextStart;
        var weight = 0.0;
        while (cursor > previousStart && weight < options.OverlapTokens)
        {
            cursor--;
            weight += text[cursor] < 128 ? 0.25 : 0.5;
        }

        while (cursor < nextStart && !char.IsWhiteSpace(text[cursor]))
            cursor++;

        return cursor;
    }

    private static double Weight(string text, int start, int end) =>
        TokenEstimator.Weight(text.AsSpan(start, end - start));

    private static (int Start, int End) Trim(string text, int start, int end)
    {
        while (start < end && char.IsWhiteSpace(text[start]))
            start++;
        while (end > start && char.IsWhiteSpace(text[end - 1]))
            end--;
        return (start, end);
    }
}
