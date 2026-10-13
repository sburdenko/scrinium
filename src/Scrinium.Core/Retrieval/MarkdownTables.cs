using System.Text;

namespace Scrinium.Core.Retrieval;

public static class MarkdownTables
{
    public static string Flatten(string text)
    {
        var lines = text.Split('\n');
        var output = new StringBuilder();
        var index = 0;

        while (index < lines.Length)
        {
            if (!StartsTable(lines, index))
            {
                output.Append(lines[index]);
                index++;
                if (index < lines.Length)
                    output.Append('\n');
                continue;
            }

            var headers = Cells(lines[index]);
            index += 2;
            var first = true;
            while (index < lines.Length && IsRow(lines[index]))
            {
                if (!first)
                    output.Append('\n');
                output.Append(FlattenRow(headers, Cells(lines[index])));
                first = false;
                index++;
            }

            if (index < lines.Length)
                output.Append('\n');
        }

        return output.ToString();
    }

    private static bool StartsTable(string[] lines, int index) =>
        index + 1 < lines.Length && IsRow(lines[index]) && IsSeparator(lines[index + 1]);

    private static bool IsRow(string line) => line.TrimStart().StartsWith('|');

    private static bool IsSeparator(string line)
    {
        var trimmed = line.Trim();
        return trimmed.StartsWith('|')
            && trimmed.Contains('-')
            && trimmed.All(c => c is '|' or '-' or ':' or ' ');
    }

    private static List<string> Cells(string line)
    {
        var trimmed = line.Trim().Trim('|');
        return trimmed.Split('|').Select(cell => cell.Trim()).ToList();
    }

    private static string FlattenRow(List<string> headers, List<string> cells)
    {
        var label = cells.Count > 0 ? cells[0] : "";
        var pairs = new List<string>();
        for (var column = 1; column < cells.Count; column++)
        {
            var header = column < headers.Count ? headers[column] : $"column {column + 1}";
            pairs.Add($"{header} = {cells[column]}");
        }

        return $"{label}: {string.Join("; ", pairs)}.";
    }
}
