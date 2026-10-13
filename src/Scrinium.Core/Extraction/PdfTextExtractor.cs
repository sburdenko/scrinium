using Scrinium.Core.Documents;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Scrinium.Core.Extraction;

public sealed class PdfTextExtractor : ITextExtractor
{
    public bool Supports(DocumentKind kind) => kind is DocumentKind.Pdf;

    public IReadOnlyList<TextSegment> Extract(string path)
    {
        using var pdf = PdfDocument.Open(path);
        var segments = pdf.GetPages()
            .Select(page => new TextSegment(page.Number, ContentOrderTextExtractor.GetText(page).Replace("\r\n", "\n")))
            .Where(segment => !string.IsNullOrWhiteSpace(segment.Text))
            .ToList();

        if (segments.Count == 0)
            throw new TextExtractionException("This PDF has no text layer, so it looks like a scan. OCR arrives in v0.4.");

        return segments;
    }
}
