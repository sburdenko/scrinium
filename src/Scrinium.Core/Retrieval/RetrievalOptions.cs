namespace Scrinium.Core.Retrieval;

public sealed record RetrievalOptions(
    int TopK = 6,
    int ContextTokenBudget = 3000,
    int PreviewLength = 220,
    double MinScore = 0.35,
    double ScoreMargin = 0.10,
    SearchMode Search = SearchMode.Hybrid);
