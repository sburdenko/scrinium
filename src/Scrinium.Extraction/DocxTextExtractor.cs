using System.IO.Packaging;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Scrinium.Core.Documents;
using Scrinium.Core.Extraction;

namespace Scrinium.Extraction;

/// <summary>Reads a Word document as Markdown: heading styles become headings, lists, tables and code stay whole.</summary>
public sealed class DocxTextExtractor : ITextExtractor
{
    public bool Supports(DocumentKind kind) => kind is DocumentKind.Docx;

    public IReadOnlyList<TextSegment> Extract(string path)
    {
        try
        {
            using var document = WordprocessingDocument.Open(path, isEditable: false);
            var main = document.MainDocumentPart
                ?? throw new TextExtractionException("This file is not a readable Word document: it has no main part.");
            var body = main.Document?.Body;
            if (body is null)
                return [];

            var reader = new DocxReader(WordStyles.From(main), WordNumbering.From(main));
            return [new TextSegment(null, string.Join("\n\n", reader.Blocks(body)))];
        }
        catch (Exception ex) when (ex is OpenXmlPackageException or InvalidDataException or FileFormatException)
        {
            throw new TextExtractionException($"This file is not a readable Word document: {ex.Message}");
        }
    }

    private sealed class DocxReader(WordStyles styles, WordNumbering numbering)
    {
        public IEnumerable<string> Blocks(Body body) => Merge(Elements(body).Select(Classify).OfType<Piece>());

        private static IEnumerable<OpenXmlElement> Elements(OpenXmlElement container) =>
            container.ChildElements.SelectMany(child => child is SdtBlock { SdtContentBlock: { } content }
                ? Elements(content)
                : [child]);

        private Piece? Classify(OpenXmlElement block) => block switch
        {
            Paragraph paragraph when styles.HeadingLevel(paragraph) is { } level =>
                Block(DocxText.Of(paragraph).Trim() is { Length: > 0 } title ? new string('#', level) + " " + title : ""),
            Paragraph paragraph when styles.IsCode(paragraph) => new Piece(PieceKind.CodeLine, DocxText.Of(paragraph)),
            Paragraph paragraph when numbering.Marker(paragraph, styles) is { } marker =>
                new Piece(PieceKind.ListItem, marker + DocxText.Of(paragraph).Trim()),
            Paragraph paragraph => Block(DocxText.Of(paragraph).Trim()),
            Table table => Block(Table(table)),
            _ => null
        };

        private static Piece? Block(string text) => text.Length == 0 ? null : new Piece(PieceKind.Block, text);

        private static IEnumerable<string> Merge(IEnumerable<Piece> pieces)
        {
            IReadOnlyList<Piece> run = [];
            foreach (var piece in pieces)
            {
                if (run.Count > 0 && (piece.Kind != run[0].Kind || piece.Kind == PieceKind.Block))
                {
                    yield return Render(run);
                    run = [];
                }

                run = [.. run, piece];
            }

            if (run.Count > 0)
                yield return Render(run);
        }

        private static string Render(IReadOnlyList<Piece> run) => run[0].Kind switch
        {
            PieceKind.CodeLine => "```\n" + string.Join("\n", run.Select(p => p.Text)).Trim('\n') + "\n```",
            PieceKind.ListItem => string.Join("\n", run.Select(p => p.Text)),
            _ => run[0].Text
        };

        private static string Table(Table table) =>
            MarkdownTable.Render(table.Elements<TableRow>()
                .Select(row => (IReadOnlyList<string>)row.Elements<TableCell>()
                    .Select(cell => string.Join(" ", cell.Elements<Paragraph>().Select(p => DocxText.Of(p).Trim())).Trim())
                    .ToList())
                .ToList());
    }

    private enum PieceKind
    {
        Block,
        ListItem,
        CodeLine
    }

    private sealed record Piece(PieceKind Kind, string Text);
}
