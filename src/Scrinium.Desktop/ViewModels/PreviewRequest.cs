using Scrinium.Core.Documents;

namespace Scrinium.Desktop.ViewModels;

public sealed record PreviewRequest(
    SourceLocation Location,
    string Title,
    int? Number,
    IReadOnlyList<CitationViewModel> Citations)
{
    public static PreviewRequest For(CitationViewModel citation, IReadOnlyList<CitationViewModel> siblings) =>
        new(citation.Location, citation.DocumentTitle, citation.Number, siblings);

    public static PreviewRequest ForDocument(Guid documentId, string title) =>
        new(SourceLocation.Start(documentId), title, null, []);
}
