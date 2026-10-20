namespace Scrinium.Core.Retrieval;

/// <summary>What a search looked at and why each candidate was or was not sent to the model.</summary>
public sealed record RetrievalTrace(string Query, RetrievalOptions Options, IReadOnlyList<RetrievalCandidate> Candidates);

public sealed record RetrievalCandidate(string DocumentTitle, string Section, double Score, CandidateSource Source, CandidateFate Fate);

public enum CandidateSource
{
    Meaning,
    Words,
    MeaningAndWords
}

public enum CandidateFate
{
    Sent,
    BelowMinScore,
    OutsideMargin,
    BeyondTopK,
    OverBudget
}
