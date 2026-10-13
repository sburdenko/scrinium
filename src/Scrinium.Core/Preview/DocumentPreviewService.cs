using Scrinium.Core.Documents;
using Scrinium.Core.Extraction;
using Scrinium.Core.Storage;

namespace Scrinium.Core.Preview;

public sealed class DocumentPreviewService(
    IDocumentStore store,
    IEnumerable<ITextExtractor> extractors,
    DataPaths paths)
{
    private const int ContextWindow = 1500;

    private readonly IReadOnlyList<ITextExtractor> _extractors = extractors.ToList();

    public async Task<DocumentPreview?> LoadAsync(SourceLocation location, CancellationToken ct = default)
    {
        var document = await store.GetAsync(location.DocumentId, ct);
        if (document is null)
            return null;

        var originalPath = paths.Resolve(document.StoredPath);
        var extractor = _extractors.FirstOrDefault(e => e.Supports(document.Kind));
        if (extractor is null || !File.Exists(originalPath))
            return null;

        var segments = await Task.Run(() => extractor.Extract(originalPath), ct);
        var segment = segments.FirstOrDefault(s => s.Page == location.Page) ?? segments[0];

        return Slice(document.Title, segment, location, originalPath);
    }

    private static DocumentPreview Slice(string title, TextSegment segment, SourceLocation location, string originalPath)
    {
        var text = segment.Text;
        var start = Math.Clamp(location.Offset, 0, text.Length);
        var end = Math.Clamp(start + location.Length, start, text.Length);
        var from = Math.Max(0, start - ContextWindow);
        var to = Math.Min(text.Length, end + ContextWindow);

        return new DocumentPreview(
            title,
            segment.Page,
            text[from..start],
            text[start..end],
            text[end..to],
            from > 0,
            to < text.Length,
            originalPath);
    }
}
