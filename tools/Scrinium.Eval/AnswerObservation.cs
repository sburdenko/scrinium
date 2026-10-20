using Scrinium.Core.Retrieval;

namespace Scrinium.Eval;

public sealed record AnswerObservation(
    string Text,
    IReadOnlyList<string> RetrievedTitles,
    IReadOnlyList<string> CitedTitles,
    TimeSpan Elapsed,
    IReadOnlyList<string>? CitedPassages = null,
    IReadOnlyList<string>? RetrievedLines = null,
    RetrievalTrace? Trace = null)
{
    public bool Refused => CitedTitles.Count == 0;

    /// <summary>The score of the closest candidate, or none when nothing was searched or the archive was empty.</summary>
    public double? BestScore => Trace is { Candidates.Count: > 0 } trace ? trace.Candidates.Max(c => c.Score) : null;
}
