namespace Scrinium.Core.Documents;

public sealed record Document(
    Guid Id,
    string Title,
    string StoredPath,
    DocumentKind Kind,
    DateOnly FileDate,
    DateTimeOffset AddedAt,
    IndexState State);
