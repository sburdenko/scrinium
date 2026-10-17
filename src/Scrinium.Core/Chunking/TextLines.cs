namespace Scrinium.Core.Chunking;

internal static class TextLines
{
    /// <summary>Each line with the position it starts at; the line excludes its '\n' and a trailing '\r'.</summary>
    public static IEnumerable<(int Start, string Line)> Of(string text)
    {
        for (var start = 0; start < text.Length;)
        {
            var end = text.IndexOf('\n', start);
            if (end < 0)
                end = text.Length;

            yield return (start, text[start..end].TrimEnd('\r'));
            start = end + 1;
        }
    }
}
