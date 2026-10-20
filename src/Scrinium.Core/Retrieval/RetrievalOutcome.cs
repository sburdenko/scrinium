namespace Scrinium.Core.Retrieval;

public abstract record RetrievalOutcome
{
    private RetrievalOutcome(RetrievalTrace trace) => Trace = trace;

    public RetrievalTrace Trace { get; }

    public sealed record Found(IReadOnlyList<ContextFragment> Fragments, RetrievalTrace Trace) : RetrievalOutcome(Trace);

    public sealed record NothingRelevant(string ClosestTitle, double BestScore, RetrievalTrace Trace) : RetrievalOutcome(Trace);

    public sealed record ArchiveEmpty(RetrievalTrace Trace) : RetrievalOutcome(Trace);
}
