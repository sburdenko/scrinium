namespace Scrinium.Core.Documents;

public sealed record Chunk(
    Guid Id,
    Guid DocumentId,
    int Ordinal,
    int? Page,
    int Offset,
    string Text,
    DateOnly Date,
    int TokenCount,
    string Section = "");
