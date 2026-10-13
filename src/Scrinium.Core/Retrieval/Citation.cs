using Scrinium.Core.Documents;

namespace Scrinium.Core.Retrieval;

public sealed record Citation(
    int Number,
    Guid DocumentId,
    string DocumentTitle,
    int? Page,
    DateOnly Date,
    string Preview,
    SourceLocation Location);

public sealed record ContextFragment(Citation Citation, string PromptText);
