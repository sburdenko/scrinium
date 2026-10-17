using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Scrinium.Tests.Support;

/// <summary>Builds small Word documents with real styles and numbering, the way Word itself stores them.</summary>
public sealed class TestDocx
{
    private const int BulletList = 1;
    private const int NumberedList = 2;

    private readonly List<OpenXmlElement> _body = [];
    private readonly List<Style> _styles =
    [
        ParagraphStyle("Title", "Title"),
        ParagraphStyle("Heading1", "heading 1"),
        ParagraphStyle("Heading2", "heading 2"),
        ParagraphStyle("Heading3", "heading 3"),
        ParagraphStyle("Code", "Code")
    ];

    public TestDocx Style(string id, string name, int? outlineLevel = null)
    {
        var style = ParagraphStyle(id, name);
        if (outlineLevel is { } level)
            style.Append(new StyleParagraphProperties(new OutlineLevel { Val = level }));
        _styles.Add(style);
        return this;
    }

    public TestDocx Heading(int level, string text) => Styled(level == 0 ? "Title" : $"Heading{level}", text);

    public TestDocx Paragraph(string text) => Add(new Paragraph(Run(text)));

    public TestDocx Styled(string styleId, string text) =>
        Add(new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = styleId }), Run(text)));

    public TestDocx Bullet(string text, int level = 0) => ListItem(BulletList, level, text);

    public TestDocx Numbered(string text, int level = 0) => ListItem(NumberedList, level, text);

    public TestDocx Code(string line) => Styled("Code", line);

    public TestDocx Monospace(string line, string font = "Consolas") =>
        Add(new Paragraph(new Run(new RunProperties(new RunFonts { Ascii = font, HighAnsi = font }), Text(line))));

    public TestDocx Element(OpenXmlElement element) => Add(element);

    public TestDocx Table(params string[][] rows)
    {
        var table = new Table();
        foreach (var row in rows)
            table.Append(new TableRow(row.Select(cell => new TableCell(new Paragraph(Run(cell))))));
        return Add(table);
    }

    public byte[] Build()
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new Document(new Body(_body.Select(e => e.CloneNode(true))));
            main.AddNewPart<StyleDefinitionsPart>().Styles = new Styles(_styles.Select(s => s.CloneNode(true)));
            main.AddNewPart<NumberingDefinitionsPart>().Numbering = Numbering();
        }

        return stream.ToArray();
    }

    public static Run Run(string text) => new(Text(text));

    private static Text Text(string text) => new(text) { Space = SpaceProcessingModeValues.Preserve };

    private TestDocx Add(OpenXmlElement element)
    {
        _body.Add(element);
        return this;
    }

    private TestDocx ListItem(int numberingId, int level, string text) =>
        Add(new Paragraph(
            new ParagraphProperties(new NumberingProperties(
                new NumberingLevelReference { Val = level },
                new NumberingId { Val = numberingId })),
            Run(text)));

    private static Style ParagraphStyle(string id, string name) =>
        new(new StyleName { Val = name }) { Type = StyleValues.Paragraph, StyleId = id };

    private static Numbering Numbering() => new(
        Abstract(0, NumberFormatValues.Bullet),
        Abstract(1, NumberFormatValues.Decimal),
        new NumberingInstance(new AbstractNumId { Val = 0 }) { NumberID = BulletList },
        new NumberingInstance(new AbstractNumId { Val = 1 }) { NumberID = NumberedList });

    private static AbstractNum Abstract(int id, NumberFormatValues format) =>
        new(Enumerable.Range(0, 3).Select(level => new Level(
            new StartNumberingValue { Val = 1 },
            new NumberingFormat { Val = format }) { LevelIndex = level })) { AbstractNumberId = id };
}
