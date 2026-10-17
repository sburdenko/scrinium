using System.Text.RegularExpressions;

namespace Scrinium.Core.Chunking;

/// <summary>The Markdown headings of one text, used to name the section that encloses a position in it.</summary>
internal sealed partial class SectionOutline
{
    // The prompt strips '<' and '>' from attributes, so the levels cannot be joined with '>'.
    public const string Separator = " › ";

    private static readonly string[] Fences = ["```", "~~~"];

    private readonly IReadOnlyList<Heading> _headings;

    private SectionOutline(IReadOnlyList<Heading> headings) => _headings = headings;

    [GeneratedRegex(@"^(#{1,6})[ \t]+(.+?)(?:[ \t]+#+)?[ \t]*$")]
    private static partial Regex HeadingLine();

    public static SectionOutline Of(string text)
    {
        var headings = new List<Heading>();
        IReadOnlyList<Heading> open = [];
        var insideFence = false;

        foreach (var (start, line) in TextLines.Of(text))
        {
            if (Fences.Any(fence => line.TrimStart().StartsWith(fence, StringComparison.Ordinal)))
            {
                insideFence = !insideFence;
                continue;
            }

            if (insideFence || HeadingLine().Match(line) is not { Success: true } match)
                continue;

            var level = match.Groups[1].Length;
            var enclosing = open.Where(h => h.Level < level).ToList();
            var path = string.Join(Separator, enclosing.Select(h => h.Title).Append(match.Groups[2].Value));
            var parent = string.Join(Separator, enclosing.Select(h => h.Title));
            var heading = new Heading(start, level, match.Groups[2].Value, path, parent);
            open = [.. enclosing, heading];
            headings.Add(heading);
        }

        return new SectionOutline(headings);
    }

    /// <summary>
    /// The section a chunk starting at <paramref name="position"/> belongs to: the parents of a heading that starts there,
    /// otherwise the path of the last heading before it, or "" outside any section.
    /// </summary>
    public string At(int position) =>
        _headings.FirstOrDefault(h => h.Position == position)?.ParentPath
        ?? _headings.LastOrDefault(h => h.Position < position)?.Path
        ?? "";

    private sealed record Heading(int Position, int Level, string Title, string Path, string ParentPath);
}
