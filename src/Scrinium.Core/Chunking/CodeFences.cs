using Scrinium.Core.Documents;

namespace Scrinium.Core.Chunking;

/// <summary>Splits Markdown into prose and fenced code, the fence lines belonging to the code.</summary>
internal static class CodeFences
{
    private const int MinFenceLength = 3;

    public static IReadOnlyList<TextRegion> Split(string text)
    {
        var regions = new List<TextRegion>();
        var proseStart = 0;
        var open = (Start: -1, Fence: "");

        foreach (var (start, line) in TextLines.Of(text))
        {
            var trimmed = line.Trim();
            if (open.Start < 0 && Opening(trimmed) is { } fence)
            {
                regions.Add(new TextRegion(proseStart, start, ChunkContent.Prose));
                open = (start, fence);
            }
            else if (open.Start >= 0 && IsClosing(trimmed, open.Fence))
            {
                var end = start + line.Length;
                regions.Add(new TextRegion(open.Start, end, ChunkContent.Code));
                proseStart = end;
                open = (-1, "");
            }
        }

        regions.Add(open.Start >= 0
            ? new TextRegion(open.Start, text.Length, ChunkContent.Code)
            : new TextRegion(proseStart, text.Length, ChunkContent.Prose));

        return regions.Where(r => !string.IsNullOrWhiteSpace(text[r.Start..r.End])).ToList();
    }

    private static string? Opening(string line)
    {
        if (line.Length < MinFenceLength || line[0] is not ('`' or '~'))
            return null;

        var length = line.TakeWhile(c => c == line[0]).Count();
        return length >= MinFenceLength ? new string(line[0], length) : null;
    }

    private static bool IsClosing(string line, string fence) =>
        line.Length >= fence.Length && line.All(c => c == fence[0]);
}
