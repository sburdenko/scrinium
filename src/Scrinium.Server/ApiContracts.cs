namespace Scrinium.Server;

public sealed record ChatRequest(string? Question, IReadOnlyList<HistoryItem>? History);

public sealed record HistoryItem(string? Role, string? Text);

public sealed record CitationDto(
    int Number,
    Guid DocumentId,
    string Title,
    int? Page,
    string Date,
    string Preview,
    int Offset,
    int Length);

public sealed record PreviewDto(
    string Title,
    int? Page,
    string Before,
    string Highlight,
    string After,
    string Truncation);

public sealed record DocumentDto(Guid Id, string Title, string Kind, string State, int ChunkCount, string? Error);

public sealed record HealthDto(string Status, string Ollama, int Documents, int ReadyDocuments, int FailedDocuments);

public sealed record ErrorDto(string Error);
