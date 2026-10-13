namespace Scrinium.Core.Documents;

public sealed record SourceLocation(Guid DocumentId, int? Page, int Offset, int Length)
{
    public static SourceLocation Start(Guid documentId) => new(documentId, null, 0, 0);
}
