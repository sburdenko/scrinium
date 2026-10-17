using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Scrinium.Extraction;

/// <summary>The paragraph styles of one Word document, read by their built-in names so a localized Word still counts.</summary>
internal sealed partial class WordStyles
{
    private const int MaxMarkdownLevel = 6;
    private const int MaxChainLength = 16;

    private static readonly string[] CodeStyleWords = ["code", "source", "preformat", "macro", "verbatim", "listing"];

    private static readonly HashSet<string> MonospaceFonts = new(StringComparer.OrdinalIgnoreCase)
    {
        "Consolas", "Courier New", "Courier", "Menlo", "Monaco", "Lucida Console", "Cascadia Code", "Cascadia Mono",
        "Source Code Pro", "Fira Code", "Fira Mono", "JetBrains Mono", "DejaVu Sans Mono", "Liberation Mono",
        "Roboto Mono", "SF Mono", "Andale Mono", "Inconsolata", "Ubuntu Mono", "IBM Plex Mono"
    };

    private readonly IReadOnlyDictionary<string, Style> _byId;
    private readonly string? _defaultParagraphStyle;

    private WordStyles(IReadOnlyDictionary<string, Style> byId, string? defaultParagraphStyle)
    {
        _byId = byId;
        _defaultParagraphStyle = defaultParagraphStyle;
    }

    [GeneratedRegex(@"^heading\s*(\d)$", RegexOptions.IgnoreCase)]
    private static partial Regex HeadingName();

    public static WordStyles From(MainDocumentPart main)
    {
        var styles = main.StyleDefinitionsPart?.Styles?.Elements<Style>().Where(s => s.StyleId?.Value is not null).ToList() ?? [];
        var byId = styles.GroupBy(s => s.StyleId!.Value!).ToDictionary(g => g.Key, g => g.First());
        var defaultParagraph = styles.FirstOrDefault(s => s.Type?.Value == StyleValues.Paragraph && s.Default?.Value == true);
        return new WordStyles(byId, defaultParagraph?.StyleId?.Value);
    }

    public int? HeadingLevel(Paragraph paragraph)
    {
        if (paragraph.ParagraphProperties?.OutlineLevel?.Val?.Value is { } direct)
            return Level(direct);

        foreach (var style in Chain(paragraph))
        {
            var name = style.StyleName?.Val?.Value ?? "";
            if (HeadingName().Match(name) is { Success: true } match)
                return Math.Min(int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), MaxMarkdownLevel);
            if (name.Equals("Title", StringComparison.OrdinalIgnoreCase))
                return 1;
            if (style.StyleParagraphProperties?.OutlineLevel?.Val?.Value is { } outline)
                return Level(outline);
        }

        return null;
    }

    public bool IsCode(Paragraph paragraph)
    {
        if (Chain(paragraph).Any(style => IsCodeName(style.StyleName?.Val?.Value)))
            return true;

        var styleFont = Chain(paragraph).Select(s => s.StyleRunProperties?.RunFonts?.Ascii?.Value).FirstOrDefault(f => f is not null);
        var runs = paragraph.Descendants<Run>().Where(r => r.InnerText.Trim().Length > 0).ToList();
        return runs.Count > 0
            && runs.All(run => (run.RunProperties?.RunFonts?.Ascii?.Value ?? styleFont) is { } font && MonospaceFonts.Contains(font));
    }

    public NumberingProperties? Numbering(Paragraph paragraph) =>
        paragraph.ParagraphProperties?.NumberingProperties
        ?? Chain(paragraph).Select(s => s.StyleParagraphProperties?.NumberingProperties).FirstOrDefault(n => n is not null);

    private IEnumerable<Style> Chain(Paragraph paragraph)
    {
        var id = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? _defaultParagraphStyle;
        for (var step = 0; id is not null && step < MaxChainLength && _byId.TryGetValue(id, out var style); step++)
        {
            yield return style;
            id = style.BasedOn?.Val?.Value;
        }
    }

    private static int? Level(int outlineLevel) =>
        outlineLevel is >= 0 and < 9 ? Math.Min(outlineLevel + 1, MaxMarkdownLevel) : null;

    private static bool IsCodeName(string? name) =>
        name is not null && CodeStyleWords.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase));
}
