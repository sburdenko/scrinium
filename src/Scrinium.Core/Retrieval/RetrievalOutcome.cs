namespace Scrinium.Core.Retrieval;

public abstract record RetrievalOutcome
{
    private RetrievalOutcome() { }

    public sealed record Found(IReadOnlyList<ContextFragment> Fragments) : RetrievalOutcome;

    public sealed record NothingRelevant(string ClosestTitle, double BestScore) : RetrievalOutcome;

    public sealed record ArchiveEmpty : RetrievalOutcome;
}
