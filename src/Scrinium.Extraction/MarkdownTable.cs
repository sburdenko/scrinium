namespace Scrinium.Extraction;

internal static class MarkdownTable
{
    /// <summary>Renders rows of cell texts as a Markdown table whose first row is the header, or "" without rows.</summary>
    public static string Render(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var filled = rows.Where(cells => cells.Count > 0).ToList();
        if (filled.Count == 0)
            return "";

        var width = filled.Max(r => r.Count);
        var lines = filled
            .Select(cells => "| " + string.Join(" | ", cells.Select(c => c.Replace('|', '/')).Concat(Enumerable.Repeat("", width - cells.Count))) + " |")
            .ToList();
        lines.Insert(1, "| " + string.Join(" | ", Enumerable.Repeat("---", width)) + " |");
        return string.Join("\n", lines);
    }
}
